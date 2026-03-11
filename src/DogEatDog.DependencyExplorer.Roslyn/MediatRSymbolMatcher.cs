using Microsoft.CodeAnalysis;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal static class MediatRSymbolMatcher
{
    private const string ISender = "MediatR.ISender";
    private const string IMediator = "MediatR.IMediator";
    private const string IPublisher = "MediatR.IPublisher";

    private const string IRequestHandlerTwoArg = "MediatR.IRequestHandler<TRequest, TResponse>";
    private const string IRequestHandlerOneArg = "MediatR.IRequestHandler<TRequest>";
    private const string INotificationHandlerOneArg = "MediatR.INotificationHandler<TNotification>";

    public static bool IsMediatorDispatchInterface(INamedTypeSymbol typeSymbol)
    {
        var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);
        return string.Equals(name, ISender, StringComparison.Ordinal)
            || string.Equals(name, IMediator, StringComparison.Ordinal)
            || string.Equals(name, IPublisher, StringComparison.Ordinal);
    }

    public static string? TryGetRequestHandlerRequestTypeId(INamedTypeSymbol interfaceSymbol)
    {
        var originalDefinition = interfaceSymbol.OriginalDefinition;
        if (!originalDefinition.IsGenericType || interfaceSymbol.TypeArguments.Length is < 1 or > 2)
        {
            return null;
        }

        var fullName = originalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (!string.Equals(fullName, IRequestHandlerTwoArg, StringComparison.Ordinal)
            && !string.Equals(fullName, IRequestHandlerOneArg, StringComparison.Ordinal))
        {
            return null;
        }

        return interfaceSymbol.TypeArguments[0] is INamedTypeSymbol requestType
            ? SymbolUtilities.CreateTypeId(requestType)
            : null;
    }

    public static string? TryGetNotificationHandlerTypeId(INamedTypeSymbol interfaceSymbol)
    {
        var originalDefinition = interfaceSymbol.OriginalDefinition;
        if (!originalDefinition.IsGenericType || interfaceSymbol.TypeArguments.Length != 1)
        {
            return null;
        }

        var fullName = originalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (!string.Equals(fullName, INotificationHandlerOneArg, StringComparison.Ordinal))
        {
            return null;
        }

        return interfaceSymbol.TypeArguments[0] is INamedTypeSymbol notificationType
            ? SymbolUtilities.CreateTypeId(notificationType)
            : null;
    }
}
