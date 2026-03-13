using DogEatDog.DependencyExplorer.Core.Model;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DogEatDog.DependencyExplorer.Roslyn;

public sealed class RoslynWorkspaceLoader
{
    private static readonly object Sync = new();
    private static bool _registered;

    public async Task<RoslynWorkspaceContext> LoadAsync(
        WorkspaceScanOptions options,
        WorkspaceDiscoveryResult discovery,
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressUpdate>? progress = null)
    {
        EnsureMsBuildRegistered();

        var warnings = new List<ScanWarning>();
        var loadedProjects = new Dictionary<string, (Project Project, string? SolutionPath)>(StringComparer.OrdinalIgnoreCase);
        var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = "true",
            ["BuildingInsideVisualStudio"] = "true",
            ["AlwaysCompileMarkupFilesInSeparateDomain"] = "false"
        });

        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            warnings.Add(new ScanWarning("workspace-failed", args.Diagnostic.Message, null, Certainty.Ambiguous));
        });

        progress?.Report(new ScanProgressUpdate(
            "roslyn-load",
            $"Opening {discovery.Solutions.Count} solution(s) and {discovery.Projects.Count} discovered project(s).",
            0,
            discovery.Solutions.Count + discovery.Projects.Count));

        var workItemCount = discovery.Solutions.Count + discovery.Projects.Count;
        var processedWorkItems = 0;

        foreach (var (solution, solutionIndex) in discovery.Solutions.Select((value, index) => (value, index + 1)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                progress?.Report(new ScanProgressUpdate(
                    "roslyn-load",
                    $"Opening solution {solutionIndex}/{discovery.Solutions.Count}: {solution.FullPath}",
                    processedWorkItems,
                    workItemCount));

                var openedSolution = await workspace.OpenSolutionAsync(solution.FullPath, cancellationToken: cancellationToken);
                var addedProjects = 0;
                foreach (var project in openedSolution.Projects.Where(project => project.Language == LanguageNames.CSharp))
                {
                    var key = PathUtility.NormalizeAbsolutePath(project.FilePath ?? project.Name);
                    loadedProjects[key] = (project, solution.FullPath);
                    addedProjects++;
                }

                processedWorkItems++;
                progress?.Report(new ScanProgressUpdate(
                    "roslyn-load",
                    $"Loaded solution {solution.Name} with {addedProjects} C# project(s).",
                    processedWorkItems,
                    workItemCount));
            }
            catch (Exception ex)
            {
                processedWorkItems++;
                warnings.Add(new ScanWarning("solution-load-failed", ex.Message, solution.FullPath, Certainty.Ambiguous));
                progress?.Report(new ScanProgressUpdate(
                    "roslyn-load",
                    $"Failed to open solution {solution.FullPath}: {ex.Message}",
                    processedWorkItems,
                    workItemCount));
            }
        }

        foreach (var (looseProject, projectIndex) in discovery.Projects.Select((value, index) => (value, index + 1)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loadedProjects.ContainsKey(looseProject.FullPath))
            {
                processedWorkItems++;
                continue;
            }

            try
            {
                if (ShouldReportProgressStep(projectIndex, discovery.Projects.Count, 25))
                {
                    progress?.Report(new ScanProgressUpdate(
                        "roslyn-load",
                        $"Opening loose project {projectIndex}/{discovery.Projects.Count}: {looseProject.FullPath}",
                        processedWorkItems,
                        workItemCount));
                }

                var project = await workspace.OpenProjectAsync(looseProject.FullPath, cancellationToken: cancellationToken);
                loadedProjects[looseProject.FullPath] = (project, null);
                processedWorkItems++;
            }
            catch (Exception ex)
            {
                processedWorkItems++;
                warnings.Add(new ScanWarning("project-load-failed", ex.Message, looseProject.FullPath, Certainty.Ambiguous));
                progress?.Report(new ScanProgressUpdate(
                    "roslyn-load",
                    $"Failed to open project {looseProject.FullPath}: {ex.Message}",
                    processedWorkItems,
                    workItemCount));
            }
        }

        var accessor = new RoslynWorkspaceContextAccessor();
        var projectContexts = new List<RoslynProjectContext>();
        progress?.Report(new ScanProgressUpdate(
            "compilation",
            $"Building semantic models for {loadedProjects.Count} loaded project(s).",
            0,
            loadedProjects.Count));

        var compiledProjectCount = 0;

        foreach (var (_, value) in loadedProjects.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var compilation = await value.Project.GetCompilationAsync(cancellationToken);
                if (compilation is null)
                {
                    warnings.Add(new ScanWarning("compilation-null", $"Compilation for project '{value.Project.Name}' was null.", value.Project.FilePath, Certainty.Ambiguous));
                    compiledProjectCount++;
                    continue;
                }

                var repository = discovery.Repositories
                    .Where(repo => value.Project.FilePath is not null && PathUtility.IsUnderPath(value.Project.FilePath, repo.RootPath))
                    .OrderByDescending(repo => repo.RootPath.Length)
                    .FirstOrDefault();

                projectContexts.Add(new RoslynProjectContext(
                    accessor,
                    value.Project,
                    compilation,
                    value.SolutionPath,
                    repository?.Name,
                    repository?.RootPath));
                compiledProjectCount++;
                if (ShouldReportProgressStep(compiledProjectCount, loadedProjects.Count, 25))
                {
                    progress?.Report(new ScanProgressUpdate(
                        "compilation",
                        $"Compiled {compiledProjectCount}/{loadedProjects.Count}: {value.Project.Name}",
                        compiledProjectCount,
                        loadedProjects.Count));
                }
            }
            catch (Exception ex)
            {
                compiledProjectCount++;
                warnings.Add(new ScanWarning("compilation-failed", ex.Message, value.Project.FilePath, Certainty.Ambiguous));
                progress?.Report(new ScanProgressUpdate(
                    "compilation",
                    $"Failed to compile {value.Project.Name}: {ex.Message}",
                    compiledProjectCount,
                    loadedProjects.Count));
            }
        }

        var symbolCatalog = WorkspaceSymbolCatalogBuilder.Build(projectContexts, options);
        var roslynContext = new RoslynWorkspaceContext(options, discovery, projectContexts, symbolCatalog, warnings);
        accessor.Value = roslynContext;
        return roslynContext;
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (Sync)
        {
            if (_registered || MSBuildLocator.IsRegistered)
            {
                _registered = true;
                return;
            }

            MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
    }

    private static bool ShouldReportProgressStep(int current, int total, int interval) =>
        current <= 1
        || current == total
        || current % interval == 0;
}
