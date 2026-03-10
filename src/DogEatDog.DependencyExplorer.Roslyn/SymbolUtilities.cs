using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.Roslyn;

public static class SymbolUtilities
{
    public static bool IsSourceSymbol(ISymbol symbol) =>
        symbol.Locations.Any(location => location.IsInSource && !string.IsNullOrWhiteSpace(location.SourceTree?.FilePath));

    public static SourceLocation? ToSourceLocation(ISymbol symbol) =>
        symbol.Locations.Select(ToSourceLocation).FirstOrDefault(location => location is not null);

    public static SourceLocation? ToSourceLocation(SyntaxNode node) => ToSourceLocation(node.GetLocation());

    public static SourceLocation? ToSourceLocation(Location? location)
    {
        if (location is null || !location.IsInSource || string.IsNullOrWhiteSpace(location.SourceTree?.FilePath))
        {
            return null;
        }

        var span = location.GetLineSpan();
        return new SourceLocation(
            PathUtility.NormalizeAbsolutePath(span.Path),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }

    public static string CreateTypeId(INamedTypeSymbol symbol) =>
        $"type:{symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";

    public static string CreateMethodId(IMethodSymbol symbol) =>
        $"method:{symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";

    public static string CreateSyntheticMethodId(string projectName, SyntaxNode node, string nameHint) =>
        GraphIdFactory.Create("method", projectName, nameHint, node.SyntaxTree.FilePath, node.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static string GetTypeDisplayName(INamedTypeSymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    public static string GetMethodDisplayName(IMethodSymbol symbol) =>
        $"{symbol.ContainingType.Name}.{symbol.Name}";

    public static GraphNodeType ClassifyType(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.Interface)
        {
            return GraphNodeType.Interface;
        }

        if (InheritsFrom(symbol, "Microsoft.EntityFrameworkCore.DbContext"))
        {
            return GraphNodeType.DbContext;
        }

        if (InheritsFrom(symbol, "Microsoft.AspNetCore.Mvc.ControllerBase") || symbol.Name.EndsWith("Controller", StringComparison.Ordinal))
        {
            return GraphNodeType.Controller;
        }

        if (symbol.Name.EndsWith("Service", StringComparison.Ordinal)
            || symbol.Name.EndsWith("Repository", StringComparison.Ordinal)
            || symbol.Name.EndsWith("Manager", StringComparison.Ordinal)
            || symbol.Name.EndsWith("Handler", StringComparison.Ordinal)
            || symbol.Name.EndsWith("Client", StringComparison.Ordinal))
        {
            return GraphNodeType.Service;
        }

        return GraphNodeType.Implementation;
    }

    public static bool InheritsFrom(INamedTypeSymbol? symbol, string fullyQualifiedBaseType)
    {
        var current = symbol;
        while (current is not null)
        {
            if (string.Equals(current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty), fullyQualifiedBaseType, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    public static IEnumerable<MethodDeclarationSyntax> GetMethodDeclarations(SyntaxNode root) =>
        root.DescendantNodes().OfType<MethodDeclarationSyntax>();

    public static IEnumerable<ConstructorDeclarationSyntax> GetConstructorDeclarations(SyntaxNode root) =>
        root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();

    public static bool IsGeneratedFile(string filePath) =>
        filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
        || filePath.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
}
