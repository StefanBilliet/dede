using DogEatDog.DependencyExplorer.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal static class MediatRSendDispatchResolver
{
    public static MediatRSendDispatchResolution? TryResolve(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol invokedMethod,
        WorkspaceSymbolCatalog symbolCatalog,
        CancellationToken cancellationToken)
    {
        var dispatchKind = GetDispatchKind(invokedMethod);
        if (dispatchKind is null)
        {
            return null;
        }

        if (invocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        var requestExpression = invocation.ArgumentList.Arguments[0].Expression;
        var requestType = ResolveConcreteRequestType(requestExpression, semanticModel, cancellationToken);
        if (requestType is null)
        {
            return null;
        }

        var requestTypeId = SymbolUtilities.CreateTypeId(requestType);
        var handlers = string.Equals(dispatchKind, MediatRDispatchKind.Send, StringComparison.Ordinal)
            ? symbolCatalog.RequestHandlerMethodsByRequestTypeId.GetValueOrDefault(requestTypeId, [])
            : symbolCatalog.NotificationHandlerMethodsByNotificationTypeId.GetValueOrDefault(requestTypeId, []);

        var methodDisplay = invokedMethod.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var requestDisplay = requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);

        return new MediatRSendDispatchResolution(requestType, requestTypeId, requestDisplay, methodDisplay, dispatchKind, handlers);
    }

    private static string? GetDispatchKind(IMethodSymbol methodSymbol)
    {
        var methodName = methodSymbol.Name;
        if (!string.Equals(methodName, "Send", StringComparison.Ordinal)
            && !string.Equals(methodName, "Publish", StringComparison.Ordinal))
        {
            return null;
        }

        var isMediatRMethod = IsMediatRInterface(methodSymbol.ContainingType)
            || methodSymbol.ContainingType.AllInterfaces.Any(IsMediatRInterface);

        if (!isMediatRMethod)
        {
            return null;
        }

        return string.Equals(methodName, "Send", StringComparison.Ordinal)
            ? MediatRDispatchKind.Send
            : MediatRDispatchKind.Publish;
    }

    private static bool IsMediatRInterface(INamedTypeSymbol typeSymbol)
    {
        var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);
        return string.Equals(name, "MediatR.ISender", StringComparison.Ordinal)
            || string.Equals(name, "MediatR.IMediator", StringComparison.Ordinal)
            || string.Equals(name, "MediatR.IPublisher", StringComparison.Ordinal);
    }

    private static INamedTypeSymbol? ResolveConcreteRequestType(
        ExpressionSyntax requestExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var inferredType = semanticModel.GetTypeInfo(requestExpression, cancellationToken).Type as INamedTypeSymbol;
        if (IsConcreteRequestType(inferredType))
        {
            return inferredType;
        }

        if (requestExpression is CastExpressionSyntax castExpression)
        {
            var castInnerType = semanticModel.GetTypeInfo(castExpression.Expression, cancellationToken).Type as INamedTypeSymbol;
            if (IsConcreteRequestType(castInnerType))
            {
                return castInnerType;
            }
        }

        if (requestExpression is IdentifierNameSyntax
            && semanticModel.GetSymbolInfo(requestExpression, cancellationToken).Symbol is ILocalSymbol localSymbol)
        {
            foreach (var syntaxReference in localSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax { Initializer.Value: { } initializerValue })
                {
                    continue;
                }

                var initializerType = semanticModel.GetTypeInfo(initializerValue, cancellationToken).Type as INamedTypeSymbol;
                if (IsConcreteRequestType(initializerType))
                {
                    return initializerType;
                }
            }
        }

        return null;
    }

    private static bool IsConcreteRequestType(INamedTypeSymbol? symbol) =>
        symbol is not null
        && symbol.TypeKind is not (TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Error);
}

internal sealed record MediatRSendDispatchResolution(
    INamedTypeSymbol RequestTypeSymbol,
    string RequestTypeId,
    string RequestTypeDisplayName,
    string MediatorMethodDisplayName,
    string DispatchKind,
    IReadOnlyList<MethodReference> HandlerMethods)
{
    public Certainty DetermineCertainty()
    {
        var certainty = HandlerMethods.Count == 1 ? Certainty.Inferred : Certainty.Ambiguous;
        if (string.Equals(DispatchKind, MediatRDispatchKind.Publish, StringComparison.Ordinal))
        {
            certainty = Certainty.Exact;
        }

        return certainty;
    }
};

internal static class MediatRDispatchKind
{
    public const string Send = "send";
    public const string Publish = "publish";
}
