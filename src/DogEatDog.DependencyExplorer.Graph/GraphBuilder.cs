using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph.Model;

namespace DogEatDog.DependencyExplorer.Graph;

public sealed class GraphBuilder
{
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GraphEdge> _edges = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<GraphNode> Nodes => _nodes.Values;

    public IReadOnlyCollection<GraphEdge> Edges => _edges.Values;

    public GraphNode AddNode(
        string id,
        GraphNodeType type,
        string displayName,
        SourceLocation? sourceLocation = null,
        string? repositoryName = null,
        string? projectName = null,
        Certainty certainty = Certainty.Exact,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        var candidate = new GraphNode(
            id,
            type,
            displayName,
            sourceLocation,
            repositoryName,
            projectName,
            certainty,
            metadata ?? new Dictionary<string, string?>());

        if (_nodes.TryGetValue(id, out var existing))
        {
            var merged = existing with
            {
                Type = SelectMoreSpecificNodeType(existing.Type, candidate.Type),
                DisplayName = string.IsNullOrWhiteSpace(existing.DisplayName) ? candidate.DisplayName : existing.DisplayName,
                SourceLocation = existing.SourceLocation ?? candidate.SourceLocation,
                RepositoryName = existing.RepositoryName ?? candidate.RepositoryName,
                ProjectName = existing.ProjectName ?? candidate.ProjectName,
                Certainty = SelectHigherCertainty(existing.Certainty, candidate.Certainty),
                Metadata = MergeMetadata(existing.Metadata, candidate.Metadata)
            };

            _nodes[id] = merged;
            return merged;
        }

        _nodes[id] = candidate;
        return candidate;
    }

    public GraphEdge AddEdge(
        string sourceId,
        string targetId,
        GraphEdgeType type,
        string displayName,
        SourceLocation? sourceLocation = null,
        string? repositoryName = null,
        string? projectName = null,
        Certainty certainty = Certainty.Exact,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        var edgeId = GraphIdFactory.Create("edge", sourceId, targetId, type.ToString(), displayName);
        var candidate = new GraphEdge(
            edgeId,
            sourceId,
            targetId,
            type,
            displayName,
            sourceLocation,
            repositoryName,
            projectName,
            certainty,
            metadata ?? new Dictionary<string, string?>());

        if (_edges.TryGetValue(edgeId, out var existing))
        {
            var merged = existing with
            {
                SourceLocation = existing.SourceLocation ?? candidate.SourceLocation,
                RepositoryName = existing.RepositoryName ?? candidate.RepositoryName,
                ProjectName = existing.ProjectName ?? candidate.ProjectName,
                Certainty = SelectHigherCertainty(existing.Certainty, candidate.Certainty),
                Metadata = MergeMetadata(existing.Metadata, candidate.Metadata)
            };

            _edges[edgeId] = merged;
            return merged;
        }

        _edges[edgeId] = candidate;
        return candidate;
    }

    public GraphDocument Build(ScanMetadata scanMetadata, IReadOnlyList<ScanWarning> warnings)
    {
        var nodes = _nodes.Values.OrderBy(node => node.Type).ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var edges = _edges.Values.OrderBy(edge => edge.Type).ThenBy(edge => edge.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var unresolved = warnings.Where(warning => warning.Certainty is Certainty.Unresolved or Certainty.Ambiguous).ToArray();

        var statistics = new ScanStatistics(
            RepositoryCount: nodes.Count(node => node.Type == GraphNodeType.Repository),
            SolutionCount: nodes.Count(node => node.Type == GraphNodeType.Solution),
            ProjectCount: nodes.Count(node => node.Type == GraphNodeType.Project),
            EndpointCount: nodes.Count(node => node.Type == GraphNodeType.Endpoint),
            MethodCount: nodes.Count(node => node.Type == GraphNodeType.Method),
            HttpEdgeCount: edges.Count(edge => edge.Type is GraphEdgeType.CALLS_HTTP or GraphEdgeType.USES_HTTP_CLIENT),
            TableCount: nodes.Count(node => node.Type == GraphNodeType.Table),
            CrossRepoLinkCount: edges.Count(edge => edge.Type == GraphEdgeType.CROSSES_REPO_BOUNDARY),
            AmbiguousEdgeCount: edges.Count(edge => edge.Certainty is Certainty.Ambiguous or Certainty.Unresolved));

        return new GraphDocument(nodes, edges, scanMetadata, warnings, unresolved, statistics);
    }

    private static GraphNodeType SelectMoreSpecificNodeType(GraphNodeType left, GraphNodeType right) =>
        NodeTypePriority(left) >= NodeTypePriority(right) ? left : right;

    private static int NodeTypePriority(GraphNodeType type) => type switch
    {
        GraphNodeType.Endpoint => 100,
        GraphNodeType.Controller => 90,
        GraphNodeType.DbContext => 85,
        GraphNodeType.Entity => 84,
        GraphNodeType.Table => 83,
        GraphNodeType.ExternalEndpoint => 82,
        GraphNodeType.ExternalService => 81,
        GraphNodeType.HttpClient => 80,
        GraphNodeType.Method => 70,
        GraphNodeType.Interface => 60,
        GraphNodeType.Service => 55,
        GraphNodeType.Implementation => 50,
        GraphNodeType.Project => 40,
        GraphNodeType.Solution => 30,
        GraphNodeType.Repository => 20,
        GraphNodeType.Workspace => 10,
        GraphNodeType.ConfigurationKey => 45,
        _ => 0
    };

    private static Certainty SelectHigherCertainty(Certainty left, Certainty right) =>
        left >= right ? left : right;

    private static IReadOnlyDictionary<string, string?> MergeMetadata(
        IReadOnlyDictionary<string, string?> left,
        IReadOnlyDictionary<string, string?> right)
    {
        if (left.Count == 0)
        {
            return new Dictionary<string, string?>(right);
        }

        var merged = new Dictionary<string, string?>(left, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in right)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!merged.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                merged[key] = value;
            }
            else if (!string.IsNullOrWhiteSpace(value) && !string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
            {
                merged[key] = $"{existing} | {value}";
            }
        }

        return merged;
    }
}
