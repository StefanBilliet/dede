using DogEatDog.DependencyExplorer.AspNet;
using DogEatDog.DependencyExplorer.EFCore;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using DogEatDog.DependencyExplorer.HttpDiscovery;
using DogEatDog.DependencyExplorer.Roslyn;

namespace DogEatDog.DependencyExplorer.IntegrationTests;

public sealed class DependencyExplorerIntegrationTests
{
    [Fact]
    public async Task ScanWorkspace_ProducesStableGraphCountsAndCriticalEdges()
    {
        var graph = await IntegrationWorkspace.GetGraphAsync();

        Assert.Equal(2, graph.Statistics.RepositoryCount);
        Assert.True(graph.Statistics.ProjectCount >= 5);
        Assert.True(graph.Statistics.EndpointCount >= 2);
        Assert.True(graph.Statistics.CrossRepoLinkCount >= 1);
        Assert.True(graph.Statistics.TableCount >= 1);

        Assert.Contains(graph.Edges, edge => edge.Type == GraphEdgeType.CROSSES_REPO_BOUNDARY);
        Assert.Contains(graph.Nodes, node => node.Type == GraphNodeType.ExternalEndpoint);
    }

    [Fact]
    public async Task BlastRadiusQueries_WalkDownstreamAndUpstream()
    {
        var graph = await IntegrationWorkspace.GetGraphAsync();
        var engine = new GraphQueryEngine(graph);
        var table = graph.Nodes.Single(node => node.Type == GraphNodeType.Table);

        var downstream = engine.GetImpactSubgraph("GET /api/Orders/{id}", upstream: false, maxDepth: 12);
        var upstream = engine.GetImpactSubgraph(table.Id, upstream: true, maxDepth: 12);

        Assert.Contains(downstream.Nodes, node => node.Type == GraphNodeType.ExternalEndpoint && node.DisplayName.Contains("/products", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(upstream.Nodes, node => node.Type == GraphNodeType.Method && node.DisplayName == "OrderRepository.Load");
    }
}

internal static class IntegrationWorkspace
{
    private static readonly Lazy<Task<DogEatDog.DependencyExplorer.Graph.Model.GraphDocument>> LazyGraph = new(CreateAsync);

    public static Task<DogEatDog.DependencyExplorer.Graph.Model.GraphDocument> GetGraphAsync() => LazyGraph.Value;

    private static Task<DogEatDog.DependencyExplorer.Graph.Model.GraphDocument> CreateAsync()
    {
        var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/Fixtures/MultiRepoWorkspace"));
        var scanner = new DependencyExplorerScanner(
            contributors: [new AspNetGraphContributor(), new HttpGraphContributor(), new EfCoreGraphContributor()]);

        return scanner.ScanAsync(rootPath);
    }
}
