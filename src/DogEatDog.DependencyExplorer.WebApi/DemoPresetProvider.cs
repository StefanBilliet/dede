namespace DogEatDog.DependencyExplorer.WebApi;

public sealed class DemoPresetProvider
{
    public IReadOnlyList<object> GetPresets() =>
    [
        new
        {
            id = "table-impact",
            title = "Show Endpoints Touching a Selected Table",
            description = "Start from a table node and walk upstream to controllers, minimal APIs, and callers.",
            direction = "upstream"
        },
        new
        {
            id = "cross-repo",
            title = "Show Cross-Repo Dependencies",
            description = "Filter the graph to edges that cross repository boundaries.",
            direction = "downstream"
        },
        new
        {
            id = "endpoint-downstream",
            title = "Show All Downstream Dependencies of an Endpoint",
            description = "Trace endpoint to handler, services, repositories, entities, tables, and HTTP calls.",
            direction = "downstream"
        },
        new
        {
            id = "mediatr-dispatch",
            title = "Show MediatR Dispatch Chains",
            description = "Filter to request dispatch and handler-resolution edges to inspect endpoint-to-handler flow.",
            direction = "downstream"
        },
        new
        {
            id = "service-callers",
            title = "Show All Upstream Callers of a Service or Method",
            description = "Walk inbound edges to reveal callers, entry points, and repos affected by a change.",
            direction = "upstream"
        },
        new
        {
            id = "ambiguity-audit",
            title = "Show Ambiguous Edges Requiring Human Review",
            description = "Audit all non-exact edges and unresolved static links before making a change.",
            direction = "downstream"
        }
    ];
}
