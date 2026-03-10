using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph.Model;

namespace DogEatDog.DependencyExplorer.Graph;

public sealed class GraphQueryEngine
{
    private readonly GraphDocument _graph;
    private readonly Dictionary<string, GraphNode> _nodesById;
    private readonly Dictionary<string, List<GraphEdge>> _outgoing;
    private readonly Dictionary<string, List<GraphEdge>> _incoming;

    public GraphQueryEngine(GraphDocument graph)
    {
        _graph = graph;
        _nodesById = graph.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        _outgoing = graph.Edges.GroupBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        _incoming = graph.Edges.GroupBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public GraphNode ResolveNode(string idOrName)
    {
        if (_nodesById.TryGetValue(idOrName, out var exact))
        {
            return exact;
        }

        var byName = _graph.Nodes.FirstOrDefault(node => string.Equals(node.DisplayName, idOrName, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return byName;
        }

        var fuzzy = _graph.Nodes.FirstOrDefault(node => node.DisplayName.Contains(idOrName, StringComparison.OrdinalIgnoreCase));
        return fuzzy ?? throw new InvalidOperationException($"No node matched '{idOrName}'.");
    }

    public GraphSubgraph GetImpactSubgraph(
        string idOrName,
        bool upstream,
        int maxDepth = 6,
        bool exactOnly = false,
        bool includeAmbiguous = true)
    {
        var focus = ResolveNode(idOrName);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { focus.Id };
        var pending = new Queue<(string NodeId, int Depth)>();
        pending.Enqueue((focus.Id, 0));
        var edges = new List<GraphEdge>();

        while (pending.Count > 0)
        {
            var (currentNodeId, depth) = pending.Dequeue();
            if (depth >= maxDepth)
            {
                continue;
            }

            var candidates = upstream
                ? _incoming.GetValueOrDefault(currentNodeId, [])
                : _outgoing.GetValueOrDefault(currentNodeId, []);

            foreach (var edge in candidates.Where(edge => IncludeEdge(edge, exactOnly, includeAmbiguous)))
            {
                edges.Add(edge);
                var nextNodeId = upstream ? edge.SourceId : edge.TargetId;
                if (visited.Add(nextNodeId))
                {
                    pending.Enqueue((nextNodeId, depth + 1));
                }
            }
        }

        var nodes = visited.Select(nodeId => _nodesById[nodeId]).OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        return new GraphSubgraph(focus, nodes, edges.DistinctBy(edge => edge.Id).ToArray());
    }

    public GraphPathResult FindPaths(
        string fromIdOrName,
        string toIdOrName,
        int maxDepth = 8,
        bool exactOnly = false,
        bool includeAmbiguous = false)
    {
        var from = ResolveNode(fromIdOrName);
        var to = ResolveNode(toIdOrName);
        var results = new List<GraphPath>();

        var nodePath = new List<GraphNode> { from };
        var edgePath = new List<GraphEdge>();

        DepthFirstSearch(from.Id, to.Id, maxDepth, nodePath, edgePath, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from.Id });
        return new GraphPathResult(from, to, results);

        void DepthFirstSearch(
            string currentId,
            string destinationId,
            int remainingDepth,
            List<GraphNode> currentNodes,
            List<GraphEdge> currentEdges,
            HashSet<string> visited)
        {
            if (remainingDepth < 0)
            {
                return;
            }

            if (string.Equals(currentId, destinationId, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new GraphPath(currentNodes.ToArray(), currentEdges.ToArray()));
                return;
            }

            foreach (var edge in _outgoing.GetValueOrDefault(currentId, []))
            {
                if (!IncludeEdge(edge, exactOnly, includeAmbiguous))
                {
                    continue;
                }

                if (!visited.Add(edge.TargetId))
                {
                    continue;
                }

                currentEdges.Add(edge);
                currentNodes.Add(_nodesById[edge.TargetId]);
                DepthFirstSearch(edge.TargetId, destinationId, remainingDepth - 1, currentNodes, currentEdges, visited);
                currentEdges.RemoveAt(currentEdges.Count - 1);
                currentNodes.RemoveAt(currentNodes.Count - 1);
                visited.Remove(edge.TargetId);
            }
        }
    }

    private static bool IncludeEdge(GraphEdge edge, bool exactOnly, bool includeAmbiguous)
    {
        if (exactOnly && edge.Certainty != Certainty.Exact)
        {
            return false;
        }

        if (!includeAmbiguous && edge.Certainty is Certainty.Ambiguous or Certainty.Unresolved)
        {
            return false;
        }

        return true;
    }

    public GraphAmbiguousReview GetAmbiguousReview()
    {
        var unresolvedEdges = _graph.Edges
            .Where(edge => edge.Certainty == Certainty.Unresolved)
            .OrderBy(edge => edge.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ambiguousEdges = _graph.Edges
            .Where(edge => edge.Certainty == Certainty.Ambiguous)
            .OrderBy(edge => edge.Type.ToString())
            .ThenBy(edge => edge.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new GraphAmbiguousReview(unresolvedEdges, ambiguousEdges, _graph.Unresolved);
    }
}
