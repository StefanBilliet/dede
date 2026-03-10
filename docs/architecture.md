# Architecture

## Scan Pipeline

```mermaid
flowchart TD
    A[Root path] --> B[FileSystemWorkspaceDiscoverer]
    B --> C[Repositories]
    B --> D[Solutions]
    B --> E[Projects]
    B --> F[AppSettings / Config keys]
    D --> G[RoslynWorkspaceLoader]
    E --> G
    G --> H[ProjectGraphContributor]
    G --> I[AspNetGraphContributor]
    G --> J[HttpGraphContributor]
    G --> K[EfCoreGraphContributor]
    H --> L[GraphBuilder]
    I --> L
    J --> L
    K --> L
    L --> M[GraphDocument]
```

## Query Flow

```mermaid
flowchart LR
    GraphJson[Graph JSON] --> QueryEngine[GraphQueryEngine]
    QueryEngine --> Impact[Impact subgraph]
    QueryEngine --> Paths[Path search]
    Impact --> Cli[CLI output]
    Impact --> Api[Web API]
    Paths --> Cli
    Paths --> Api
    Api --> Ui[Web UI]
```

## Notes

- `Core` owns scan options, certainty classes, discovery records, and filesystem scanning.
- `Graph` owns the normalized schema, graph builder, and blast-radius query engine.
- `Roslyn` owns MSBuild/Roslyn loading, symbol identity, topology wiring, and base method graph extraction.
- `AspNet`, `HttpDiscovery`, and `EFCore` add specialized contributors on top of Roslyn compilations.
- `Export` owns JSON and Neo4j-oriented CSV export.
- `WebApi` hosts scan/load/query endpoints and serves the UI.
- `WebUi` is a static web asset library consumed by `WebApi`.
