using System.Security.Cryptography;
using System.Text;
using DogEatDog.DependencyExplorer.Core.Model;

namespace DogEatDog.DependencyExplorer.Graph.Model;

public enum GraphNodeType
{
    Workspace,
    Repository,
    Solution,
    Project,
    Service,
    Controller,
    Endpoint,
    Method,
    Interface,
    Implementation,
    HttpClient,
    ExternalService,
    ExternalEndpoint,
    DbContext,
    Entity,
    Table,
    ConfigurationKey
}

public enum GraphEdgeType
{
    CONTAINS,
    DEFINES,
    EXPOSES,
    IMPLEMENTS,
    INJECTS,
    CALLS,
    DISPATCHES,
    HANDLED_BY,
    USES_HTTP_CLIENT,
    CALLS_HTTP,
    RESOLVES_TO_SERVICE,
    USES_DBCONTEXT,
    QUERIES_ENTITY,
    MAPS_TO_TABLE,
    DEPENDS_ON,
    CROSSES_REPO_BOUNDARY,
    MEDIATR_DISPATCHES,
    AMBIGUOUS
}

public sealed record GraphNode(
    string Id,
    GraphNodeType Type,
    string DisplayName,
    SourceLocation? SourceLocation,
    string? RepositoryName,
    string? ProjectName,
    Certainty Certainty,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record GraphEdge(
    string Id,
    string SourceId,
    string TargetId,
    GraphEdgeType Type,
    string DisplayName,
    SourceLocation? SourceLocation,
    string? RepositoryName,
    string? ProjectName,
    Certainty Certainty,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record GraphDocument(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    ScanMetadata ScanMetadata,
    IReadOnlyList<ScanWarning> Warnings,
    IReadOnlyList<ScanWarning> Unresolved,
    ScanStatistics Statistics);

public sealed record GraphSubgraph(
    GraphNode FocusNode,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

public sealed record GraphPath(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

public sealed record GraphPathResult(
    GraphNode From,
    GraphNode To,
    IReadOnlyList<GraphPath> Paths);

public sealed record GraphAmbiguousReview(
    IReadOnlyList<GraphEdge> UnresolvedEdges,
    IReadOnlyList<GraphEdge> AmbiguousEdges,
    IReadOnlyList<ScanWarning> UnresolvedSymbols);

public static class GraphIdFactory
{
    public static string Create(string prefix, params string?[] parts)
    {
        var payload = string.Join("|", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"{prefix}:{Convert.ToHexString(bytes[..8]).ToLowerInvariant()}";
    }
}
