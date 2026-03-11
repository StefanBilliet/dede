using DogEatDog.DependencyExplorer.AspNet;
using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.EFCore;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using DogEatDog.DependencyExplorer.HttpDiscovery;
using DogEatDog.DependencyExplorer.Roslyn;

namespace DogEatDog.DependencyExplorer.UnitTests;

public sealed class FixtureWorkspaceTests
{
    [Fact]
    public async Task DiscoveryAndRouteExtraction_Work_OnFixtureWorkspace()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        Assert.Contains(graph.Nodes, node => node.Type == GraphNodeType.Repository && node.DisplayName == "repo-orders");
        Assert.Contains(graph.Nodes, node => node.Type == GraphNodeType.Repository && node.DisplayName == "repo-catalog");
        Assert.Contains(graph.Nodes, node => node.Type == GraphNodeType.Solution && node.DisplayName == "Orders");
        Assert.Contains(graph.Nodes, node => node.Type == GraphNodeType.Endpoint && node.DisplayName == "GET /api/Orders/{id}");
    }

    [Fact]
    public async Task MethodCallAndDiResolution_AreCaptured()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var controllerMethod = FindNode(graph, GraphNodeType.Method, "OrdersController.Get");
        var interfaceMethod = FindNode(graph, GraphNodeType.Method, "IOrdersService.GetOrder");
        var implementationMethod = FindNode(graph, GraphNodeType.Method, "OrdersService.GetOrder");

        AssertHasEdge(graph, controllerMethod.Id, interfaceMethod.Id, GraphEdgeType.CALLS);
        AssertHasEdge(graph, controllerMethod.Id, implementationMethod.Id, GraphEdgeType.CALLS, "resolves");
    }

    [Fact]
    public async Task MediatRSendResolution_AddsDispatchEdgeForSingleHandler()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var controllerMethod = FindNode(graph, GraphNodeType.Method, "OrdersController.GetViaMediator");
        var requestNode = FindNodeByDisplayName(graph, "GetOrderQuery");
        var handlerMethod = FindNode(graph, GraphNodeType.Method, "GetOrderQueryHandler.Handle");

        var dispatchEdge = AssertHasEdge(graph, controllerMethod.Id, requestNode.Id, GraphEdgeType.DISPATCHES);
        var handledByEdge = AssertHasEdge(graph, requestNode.Id, handlerMethod.Id, GraphEdgeType.HANDLED_BY);

        Assert.Equal(Certainty.Exact, dispatchEdge.Certainty);
        Assert.Equal("MediatR", dispatchEdge.Metadata["dispatchFramework"]);
        Assert.Equal("Orders.Core.GetOrderQuery", dispatchEdge.Metadata["requestType"]);
        Assert.Equal(Certainty.Inferred, handledByEdge.Certainty);
    }

    [Fact]
    public async Task MediatRSendResolution_ResolvesLocalVariableAndGenericAsyncSend()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var requestNode = FindNodeByDisplayName(graph, "GetOrderQuery");
        var localVariableCaller = FindNode(graph, GraphNodeType.Method, "OrdersController.GetViaMediatorVariable");
        var genericAsyncCaller = FindNode(graph, GraphNodeType.Method, "OrdersController.GetViaMediatorGenericAsync");

        AssertHasEdge(graph, localVariableCaller.Id, requestNode.Id, GraphEdgeType.DISPATCHES);
        AssertHasEdge(graph, genericAsyncCaller.Id, requestNode.Id, GraphEdgeType.DISPATCHES);
    }

    [Fact]
    public async Task MediatRSendResolution_UsesAmbiguousCertaintyWhenMultipleHandlersExist()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var controllerMethod = FindNode(graph, GraphNodeType.Method, "OrdersController.GetProjectionViaMediator");
        var requestNode = FindNodeByDisplayName(graph, "GetOrderProjectionQuery");
        var primaryHandler = FindNode(graph, GraphNodeType.Method, "GetOrderProjectionPrimaryHandler.Handle");
        var secondaryHandler = FindNode(graph, GraphNodeType.Method, "GetOrderProjectionSecondaryHandler.Handle");

        var dispatchEdge = AssertHasEdge(graph, controllerMethod.Id, requestNode.Id, GraphEdgeType.DISPATCHES);
        Assert.Equal(Certainty.Exact, dispatchEdge.Certainty);

        var handledByEdges = graph.Edges
            .Where(edge => edge.SourceId == requestNode.Id && edge.Type == GraphEdgeType.HANDLED_BY)
            .ToArray();

        Assert.Equal(2, handledByEdges.Length);
        Assert.All(handledByEdges, edge => Assert.Equal(Certainty.Ambiguous, edge.Certainty));
        Assert.Contains(handledByEdges, edge => edge.TargetId == primaryHandler.Id);
        Assert.Contains(handledByEdges, edge => edge.TargetId == secondaryHandler.Id);
    }

    [Fact]
    public async Task MediatRPublishResolution_UsesExactCertaintyWhenNotificationHasMultipleHandlers()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var publisherMethod = FindNode(graph, GraphNodeType.Method, "OrdersController.PublishOrderViewed");
        var notificationNode = FindNodeByDisplayName(graph, "OrderViewedNotification");
        var primaryHandlerMethod = FindNode(graph, GraphNodeType.Method, "OrderViewedNotificationHandler.Handle");
        var auditHandlerMethod = FindNode(graph, GraphNodeType.Method, "OrderViewedAuditNotificationHandler.Handle");

        var dispatchEdge = AssertHasEdge(graph, publisherMethod.Id, notificationNode.Id, GraphEdgeType.DISPATCHES);
        var handledByEdges = graph.Edges
            .Where(edge => edge.SourceId == notificationNode.Id && edge.Type == GraphEdgeType.HANDLED_BY)
            .ToArray();

        Assert.Equal(2, handledByEdges.Length);
        Assert.Contains(handledByEdges, edge => edge.TargetId == primaryHandlerMethod.Id);
        Assert.Contains(handledByEdges, edge => edge.TargetId == auditHandlerMethod.Id);
        Assert.Equal("publish", dispatchEdge.Metadata["dispatchKind"]);
        Assert.All(handledByEdges, edge => Assert.Equal("publish", edge.Metadata["dispatchKind"]));
        Assert.All(handledByEdges, edge => Assert.Equal(Certainty.Exact, edge.Certainty));
    }

    [Fact]
    public async Task NonMediatRSendLikeMethod_DoesNotAddDispatchEdges()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var fakeDispatchCaller = FindNode(graph, GraphNodeType.Method, "OrdersController.GetViaFakeDispatcher");

        Assert.DoesNotContain(graph.Edges, edge =>
            edge.SourceId == fakeDispatchCaller.Id
            && edge.Type == GraphEdgeType.DISPATCHES);
    }

    [Fact]
    public async Task MediatRSendResolution_UsesUnresolvedCertaintyWhenNoHandlersExist()
    {
        var graph = await TestWorkspaceGraph.GetAsync();
        var caller = FindNode(graph, GraphNodeType.Method, "OrdersController.GetViaMediatorMissingHandler");
        var requestNode = FindNodeByDisplayName(graph, "MissingHandlerQuery");

        var dispatchEdge = AssertHasEdge(graph, caller.Id, requestNode.Id, GraphEdgeType.DISPATCHES);

        Assert.Equal(Certainty.Unresolved, dispatchEdge.Certainty);
        Assert.Equal("no-handler-found", dispatchEdge.Metadata["resolution"]);
    }

    [Fact]
    public async Task HttpClientDiscovery_ResolvesCrossRepoEndpoints()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var httpClient = graph.Nodes.Single(node => node.Type == GraphNodeType.HttpClient && node.DisplayName == "CatalogClient");
        var externalService = graph.Nodes.Single(node => node.Type == GraphNodeType.ExternalService && node.DisplayName == "repo-catalog");
        var externalEndpoint = graph.Nodes.Single(node => node.Type == GraphNodeType.ExternalEndpoint && node.DisplayName.Contains("/products", StringComparison.OrdinalIgnoreCase));
        var catalogEndpoint = FindNode(graph, GraphNodeType.Endpoint, "GET /products/{id}");

        AssertHasEdge(graph, httpClient.Id, externalService.Id, GraphEdgeType.CALLS_HTTP);
        AssertHasEdge(graph, externalService.Id, externalEndpoint.Id, GraphEdgeType.CALLS_HTTP);
        Assert.Contains(graph.Edges, edge => edge.Type == GraphEdgeType.CROSSES_REPO_BOUNDARY && edge.TargetId == catalogEndpoint.Id);
    }

    [Fact]
    public async Task EfEntityAndTableLineage_AreCaptured()
    {
        var graph = await TestWorkspaceGraph.GetAsync();

        var repositoryMethod = FindNode(graph, GraphNodeType.Method, "OrderRepository.Load");
        var dbContext = FindNode(graph, GraphNodeType.DbContext, "OrdersDbContext");
        var entity = FindNode(graph, GraphNodeType.Entity, "Order");
        var table = graph.Nodes.Single(node => node.Type == GraphNodeType.Table);

        AssertHasEdge(graph, repositoryMethod.Id, dbContext.Id, GraphEdgeType.USES_DBCONTEXT);
        AssertHasEdge(graph, repositoryMethod.Id, entity.Id, GraphEdgeType.QUERIES_ENTITY);
        AssertHasEdge(graph, entity.Id, table.Id, GraphEdgeType.MAPS_TO_TABLE);
    }

    [Fact]
    public async Task ExcludedPaths_AreNotScanned()
    {
        var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/Fixtures/MultiRepoWorkspace"));
        var scanner = new DependencyExplorerScanner(
            contributors: [new AspNetGraphContributor(), new HttpGraphContributor(), new EfCoreGraphContributor()]);

        var graph = await scanner.ScanAsync(
            rootPath,
            new WorkspaceScanOptions(rootPath, ExcludedPaths: ["repo-catalog"]),
            default,
            progress: null);

        Assert.DoesNotContain(graph.Nodes, node => node.Type == GraphNodeType.Repository && node.DisplayName == "repo-catalog");
        Assert.DoesNotContain(graph.Nodes, node => node.Type == GraphNodeType.Endpoint && node.DisplayName == "GET /products/{id}");
    }

    private static GraphNode FindNode(DogEatDog.DependencyExplorer.Graph.Model.GraphDocument graph, GraphNodeType type, string displayName) =>
        graph.Nodes.Single(node => node.Type == type && node.DisplayName == displayName);

    private static GraphNode FindNodeByDisplayName(DogEatDog.DependencyExplorer.Graph.Model.GraphDocument graph, string displayName) =>
        graph.Nodes.Single(node => node.DisplayName == displayName);

    private static GraphEdge AssertHasEdge(
        DogEatDog.DependencyExplorer.Graph.Model.GraphDocument graph,
        string sourceId,
        string targetId,
        GraphEdgeType edgeType,
        string? displayNameContains = null)
    {
        var edge = Assert.Single(graph.Edges, edge =>
            edge.SourceId == sourceId
            && edge.TargetId == targetId
            && edge.Type == edgeType
            && (displayNameContains is null || edge.DisplayName.Contains(displayNameContains, StringComparison.OrdinalIgnoreCase)));

        return edge;
    }
}

internal static class TestWorkspaceGraph
{
    private static readonly Lazy<Task<DogEatDog.DependencyExplorer.Graph.Model.GraphDocument>> LazyGraph = new(CreateAsync);

    public static Task<DogEatDog.DependencyExplorer.Graph.Model.GraphDocument> GetAsync() => LazyGraph.Value;

    private static Task<DogEatDog.DependencyExplorer.Graph.Model.GraphDocument> CreateAsync()
    {
        var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/Fixtures/MultiRepoWorkspace"));
        var scanner = new DependencyExplorerScanner(
            contributors: [new AspNetGraphContributor(), new HttpGraphContributor(), new EfCoreGraphContributor()]);

        return scanner.ScanAsync(rootPath);
    }
}
