using System.Diagnostics;
using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Core.Scanning;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;

namespace DogEatDog.DependencyExplorer.Roslyn;

public sealed class DependencyExplorerScanner
{
    private readonly FileSystemWorkspaceDiscoverer _discoverer;
    private readonly RoslynWorkspaceLoader _workspaceLoader;
    private readonly IReadOnlyList<IRoslynGraphContributor> _contributors;

    public DependencyExplorerScanner(
        FileSystemWorkspaceDiscoverer? discoverer = null,
        RoslynWorkspaceLoader? workspaceLoader = null,
        IEnumerable<IRoslynGraphContributor>? contributors = null)
    {
        _discoverer = discoverer ?? new FileSystemWorkspaceDiscoverer();
        _workspaceLoader = workspaceLoader ?? new RoslynWorkspaceLoader();
        _contributors = [new ProjectGraphContributor(), .. (contributors ?? [])];
    }

    public async Task<GraphDocument> ScanAsync(string rootPath, WorkspaceScanOptions? options = null, CancellationToken cancellationToken = default)
        => await ScanAsync(rootPath, options, cancellationToken, progress: null);

    public async Task<GraphDocument> ScanAsync(
        string rootPath,
        IProgress<ScanProgressUpdate>? progress,
        CancellationToken cancellationToken = default)
        => await ScanAsync(rootPath, options: null, cancellationToken, progress);

    public async Task<GraphDocument> ScanAsync(
        string rootPath,
        WorkspaceScanOptions? options,
        CancellationToken cancellationToken,
        IProgress<ScanProgressUpdate>? progress)
    {
        var startedAt = DateTimeOffset.UtcNow;
        options ??= WorkspaceScanOptions.Create(rootPath);
        var timings = new List<StageTiming>();
        progress?.Report(new ScanProgressUpdate("scan", $"Starting scan of {options.RootPath}."));

        var discoveryWatch = Stopwatch.StartNew();
        var discovery = _discoverer.Discover(options.RootPath, cancellationToken, options);
        timings.Add(new StageTiming("filesystem-discovery", discoveryWatch.Elapsed));
        progress?.Report(new ScanProgressUpdate(
            "filesystem-discovery",
            $"Discovery complete: {discovery.Repositories.Count} repos, {discovery.Solutions.Count} solutions, {discovery.Projects.Count} projects, {discovery.ConfigurationValues.Count} config keys in {discoveryWatch.Elapsed.TotalSeconds:F1}s."));

        if (options.ExcludedPaths is { Count: > 0 })
        {
            progress?.Report(new ScanProgressUpdate(
                "filesystem-discovery",
                $"Excluding {options.ExcludedPaths.Count} path pattern(s): {string.Join(", ", options.ExcludedPaths)}"));
        }

        var graphBuilder = new GraphBuilder();
        AddTopologyGraph(graphBuilder, options.RootPath, discovery);

        var loadWatch = Stopwatch.StartNew();
        var workspace = await _workspaceLoader.LoadAsync(options, discovery, cancellationToken, progress);
        timings.Add(new StageTiming("roslyn-load", loadWatch.Elapsed));
        progress?.Report(new ScanProgressUpdate(
            "roslyn-load",
            $"Roslyn load complete: {workspace.Projects.Count} project context(s) in {loadWatch.Elapsed.TotalSeconds:F1}s."));

        foreach (var contributor in _contributors)
        {
            var contributorWatch = Stopwatch.StartNew();
            progress?.Report(new ScanProgressUpdate(
                contributor.Name,
                $"Running contributor {contributor.Name} across {workspace.Projects.Count} project(s)."));
            foreach (var projectContext in workspace.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await contributor.ContributeAsync(projectContext, graphBuilder, cancellationToken);
            }

            timings.Add(new StageTiming(contributor.Name, contributorWatch.Elapsed));
            progress?.Report(new ScanProgressUpdate(
                contributor.Name,
                $"Contributor {contributor.Name} completed in {contributorWatch.Elapsed.TotalSeconds:F1}s."));
        }

        var completedAt = DateTimeOffset.UtcNow;
        var metadata = new ScanMetadata(
            options.RootPath,
            startedAt,
            completedAt,
            timings,
            new Dictionary<string, string>
            {
                ["scanner"] = nameof(DependencyExplorerScanner),
                ["dotnet"] = Environment.Version.ToString()
            });

        var warnings = discovery.Warnings.Concat(workspace.Warnings).ToArray();
        progress?.Report(new ScanProgressUpdate(
            "scan",
            $"Scan complete in {(completedAt - startedAt).TotalSeconds:F1}s with {warnings.Length} warning(s)."));
        return graphBuilder.Build(metadata, warnings);
    }

    public static string CreateWorkspaceNodeId(string rootPath) => GraphIdFactory.Create("workspace", rootPath);

    public static string CreateRepositoryNodeId(string repoPath) => GraphIdFactory.Create("repository", repoPath);

    public static string CreateSolutionNodeId(string solutionPath) => GraphIdFactory.Create("solution", solutionPath);

    public static string CreateProjectNodeId(string projectPath) => GraphIdFactory.Create("project", projectPath);

    private static void AddTopologyGraph(GraphBuilder graphBuilder, string rootPath, WorkspaceDiscoveryResult discovery)
    {
        var workspaceId = CreateWorkspaceNodeId(rootPath);
        graphBuilder.AddNode(
            workspaceId,
            GraphNodeType.Workspace,
            Path.GetFileName(rootPath),
            new SourceLocation(rootPath),
            certainty: Certainty.Exact,
            metadata: new Dictionary<string, string?> { ["rootPath"] = rootPath });

        foreach (var repository in discovery.Repositories)
        {
            var repositoryId = CreateRepositoryNodeId(repository.RootPath);
            graphBuilder.AddNode(
                repositoryId,
                GraphNodeType.Repository,
                repository.Name,
                new SourceLocation(repository.RootPath),
                repository.Name,
                certainty: Certainty.Exact,
                metadata: new Dictionary<string, string?> { ["rootPath"] = repository.RootPath });

            graphBuilder.AddEdge(workspaceId, repositoryId, GraphEdgeType.CONTAINS, $"{Path.GetFileName(rootPath)} contains {repository.Name}");
        }

        foreach (var solution in discovery.Solutions)
        {
            var solutionId = CreateSolutionNodeId(solution.FullPath);
            graphBuilder.AddNode(
                solutionId,
                GraphNodeType.Solution,
                solution.Name,
                new SourceLocation(solution.FullPath),
                solution.RepositoryName,
                certainty: Certainty.Exact,
                metadata: new Dictionary<string, string?> { ["filePath"] = solution.FullPath });

            var ownerId = solution.RepositoryPath is not null
                ? CreateRepositoryNodeId(solution.RepositoryPath)
                : workspaceId;

            graphBuilder.AddEdge(ownerId, solutionId, GraphEdgeType.CONTAINS, $"{Path.GetFileName(ownerId)} contains {solution.Name}");
        }

        foreach (var project in discovery.Projects)
        {
            var projectId = CreateProjectNodeId(project.FullPath);
            graphBuilder.AddNode(
                projectId,
                GraphNodeType.Project,
                project.Name,
                new SourceLocation(project.FullPath),
                project.RepositoryName,
                project.Name,
                Certainty.Exact,
                new Dictionary<string, string?> { ["filePath"] = project.FullPath });

            var ownerId = project.RepositoryPath is not null
                ? CreateRepositoryNodeId(project.RepositoryPath)
                : workspaceId;

            graphBuilder.AddEdge(ownerId, projectId, GraphEdgeType.CONTAINS, $"{project.RepositoryName ?? Path.GetFileName(rootPath)} contains {project.Name}");
        }

        foreach (var config in discovery.ConfigurationValues)
        {
            var configId = GraphIdFactory.Create("config", config.FilePath, config.Key);
            graphBuilder.AddNode(
                configId,
                GraphNodeType.ConfigurationKey,
                config.Key,
                new SourceLocation(config.FilePath),
                config.RepositoryName,
                certainty: Certainty.Inferred,
                metadata: new Dictionary<string, string?>
                {
                    ["value"] = config.Value,
                    ["filePath"] = config.FilePath
                });
        }
    }
}
