using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal static class MediatRDispatchEdgeContributor
{
    public static void AddIfMatch(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        IMethodSymbol caller,
        string callerId,
        IMethodSymbol targetMethod,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var resolution = MediatRSendDispatchResolver.TryResolve(
            invocation,
            semanticModel,
            targetMethod,
            projectContext.Workspace.SymbolCatalog,
            cancellationToken);

        if (resolution is null)
        {
            return;
        }

        graphBuilder.AddNode(
            resolution.RequestTypeId,
            SymbolUtilities.ClassifyType(resolution.RequestTypeSymbol),
            SymbolUtilities.GetTypeDisplayName(resolution.RequestTypeSymbol),
            SymbolUtilities.ToSourceLocation(resolution.RequestTypeSymbol),
            certainty: Certainty.Exact,
            metadata: new Dictionary<string, string?>
            {
                ["fullName"] = resolution.RequestTypeDisplayName
            });

        graphBuilder.AddEdge(
            callerId,
            resolution.RequestTypeId,
            GraphEdgeType.DISPATCHES,
            $"{caller.ContainingType.Name}.{caller.Name} dispatches {resolution.RequestTypeSymbol.Name}",
            SymbolUtilities.ToSourceLocation(invocation),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact,
            MediatRMetadata.From(resolution, GraphEdgeType.DISPATCHES).ToDictionary());

        var certainty = resolution.DetermineCertainty();
        foreach (var handlerMethod in resolution.HandlerMethods.DistinctBy(method => method.Id, StringComparer.OrdinalIgnoreCase))
        {
            graphBuilder.AddNode(
                handlerMethod.Id,
                GraphNodeType.Method,
                handlerMethod.DisplayName,
                handlerMethod.SourceLocation,
                handlerMethod.RepositoryName,
                handlerMethod.ProjectName,
                certainty);

            graphBuilder.AddEdge(
                resolution.RequestTypeId,
                handlerMethod.Id,
                GraphEdgeType.HANDLED_BY,
                $"{resolution.RequestTypeSymbol.Name} handled by {handlerMethod.DisplayName}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                certainty,
                MediatRMetadata.From(resolution, GraphEdgeType.HANDLED_BY).ToDictionary());
        }
    }
}
