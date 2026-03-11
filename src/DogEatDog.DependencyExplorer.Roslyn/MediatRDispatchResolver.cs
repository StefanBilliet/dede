using DogEatDog.DependencyExplorer.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal static class MediatRDispatchResolver
{
    public static MediatRDispatchResolution? TryResolve(
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
        var handlers = dispatchKind == MediatRDispatchKind.Send
            ? symbolCatalog.MediatR.RequestHandlerMethodsByRequestTypeId.GetValueOrDefault(requestTypeId, [])
            : symbolCatalog.MediatR.NotificationHandlerMethodsByNotificationTypeId.GetValueOrDefault(requestTypeId, []);

        var methodDisplay = invokedMethod.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var requestDisplay = requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);

        return new MediatRDispatchResolution(requestType, requestTypeId, requestDisplay, methodDisplay, dispatchKind.Value, handlers);
    }

    private static MediatRDispatchKind? GetDispatchKind(IMethodSymbol methodSymbol)
    {
        var methodName = methodSymbol.Name;
        if (!string.Equals(methodName, "Send", StringComparison.Ordinal)
            && !string.Equals(methodName, "Publish", StringComparison.Ordinal))
        {
            return null;
        }

        var isMediatRMethod = MediatRSymbolMatcher.IsMediatorDispatchInterface(methodSymbol.ContainingType)
            || methodSymbol.ContainingType.AllInterfaces.Any(MediatRSymbolMatcher.IsMediatorDispatchInterface);

        if (!isMediatRMethod)
        {
            return null;
        }

        return string.Equals(methodName, "Send", StringComparison.Ordinal)
            ? MediatRDispatchKind.Send
            : MediatRDispatchKind.Publish;
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

internal sealed record MediatRDispatchResolution(
    INamedTypeSymbol RequestTypeSymbol,
    string RequestTypeId,
    string RequestTypeDisplayName,
    string MediatorMethodDisplayName,
    MediatRDispatchKind DispatchKind,
    IReadOnlyList<MethodReference> HandlerMethods)
{
    public MediatRDispatchResolutionOutcome DetermineOutcome()
    {
        if (DispatchKind == MediatRDispatchKind.Send && HandlerMethods.Count == 0)
        {
            return MediatRDispatchResolutionOutcome.MissingHandler;
        }

        return MediatRDispatchResolutionOutcome.Resolved;
    }

    public Certainty DetermineDispatchCertainty()
    {
        return DetermineOutcome() switch
        {
            MediatRDispatchResolutionOutcome.MissingHandler => Certainty.Unresolved,
            _ => Certainty.Exact
        };
    }

    public Certainty DetermineCertainty()
    {
        var certainty = HandlerMethods.Count == 1 ? Certainty.Inferred : Certainty.Ambiguous;
        if (DispatchKind == MediatRDispatchKind.Publish)
        {
            certainty = Certainty.Exact;
        }

        return certainty;
    }
}

internal enum MediatRDispatchResolutionOutcome
{
    Resolved,
    MissingHandler
}

internal enum MediatRDispatchKind
{
    Send,
    Publish
}

internal static class MediatRDispatchKindExtensions
{
    public static string ToMetadataValue(this MediatRDispatchKind dispatchKind) => dispatchKind switch
    {
        MediatRDispatchKind.Send => "send",
        MediatRDispatchKind.Publish => "publish",
        _ => throw new ArgumentOutOfRangeException(nameof(dispatchKind), dispatchKind, null)
    };
}
