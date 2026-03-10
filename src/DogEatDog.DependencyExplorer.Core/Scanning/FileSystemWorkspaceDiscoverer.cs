using System.Collections.Immutable;
using DogEatDog.DependencyExplorer.Core.Model;

namespace DogEatDog.DependencyExplorer.Core.Scanning;

public sealed class FileSystemWorkspaceDiscoverer
{
    private static readonly ImmutableHashSet<string> IgnoredDirectories = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".git",
        ".hg",
        ".svn",
        "bin",
        "obj",
        "node_modules",
        ".idea",
        ".vs");

    public WorkspaceDiscoveryResult Discover(string rootPath, CancellationToken cancellationToken = default, WorkspaceScanOptions? options = null)
    {
        var normalizedRoot = PathUtility.NormalizeAbsolutePath(rootPath);
        options ??= WorkspaceScanOptions.Create(normalizedRoot);
        var repositories = new Dictionary<string, DiscoveredRepository>(StringComparer.OrdinalIgnoreCase);
        var solutions = new List<DiscoveredSolution>();
        var projects = new List<DiscoveredProject>();
        var configurationValues = new List<DiscoveredConfigurationValue>();
        var warnings = new List<ScanWarning>();

        if (!Directory.Exists(normalizedRoot))
        {
            warnings.Add(new ScanWarning("root-not-found", $"Root folder '{normalizedRoot}' does not exist.", normalizedRoot));
            return new WorkspaceDiscoveryResult(normalizedRoot, Array.Empty<DiscoveredRepository>(), Array.Empty<DiscoveredSolution>(), Array.Empty<DiscoveredProject>(), Array.Empty<DiscoveredConfigurationValue>(), warnings);
        }

        var pending = new Stack<string>();
        pending.Push(normalizedRoot);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pending.Pop();

            if (PathUtility.MatchesExcludedPath(currentDirectory, normalizedRoot, options.ExcludedPaths))
            {
                continue;
            }

            try
            {
                var gitMetadataPath = Path.Combine(currentDirectory, ".git");
                var repoMarkerPath = Path.Combine(currentDirectory, ".dede-repo");
                if (Directory.Exists(gitMetadataPath) || File.Exists(gitMetadataPath) || File.Exists(repoMarkerPath))
                {
                    var repo = new DiscoveredRepository(Path.GetFileName(currentDirectory), currentDirectory);
                    repositories[currentDirectory] = repo;
                }

                foreach (var filePath in Directory.EnumerateFiles(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (PathUtility.MatchesExcludedPath(filePath, normalizedRoot, options.ExcludedPaths))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(filePath);
                    var repository = FindOwningRepository(filePath, repositories.Values);

                    if (fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                    {
                        solutions.Add(new DiscoveredSolution(
                            Path.GetFileNameWithoutExtension(filePath),
                            PathUtility.NormalizeAbsolutePath(filePath),
                            repository?.Name,
                            repository?.RootPath));
                    }
                    else if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        projects.Add(new DiscoveredProject(
                            Path.GetFileNameWithoutExtension(filePath),
                            PathUtility.NormalizeAbsolutePath(filePath),
                            repository?.Name,
                            repository?.RootPath));
                    }
                    else if (fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
                             && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            foreach (var kvp in JsonUtilities.FlattenJson(filePath))
                            {
                                configurationValues.Add(new DiscoveredConfigurationValue(
                                    kvp.Key,
                                    kvp.Value,
                                    PathUtility.NormalizeAbsolutePath(filePath),
                                    repository?.Name,
                                    repository?.RootPath));
                            }
                        }
                        catch (Exception ex)
                        {
                            warnings.Add(new ScanWarning("config-parse-failed", ex.Message, filePath, Certainty.Ambiguous));
                        }
                    }
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory))
                {
                    var name = Path.GetFileName(childDirectory);
                    if (!IgnoredDirectories.Contains(name)
                        && !PathUtility.MatchesExcludedPath(childDirectory, normalizedRoot, options.ExcludedPaths))
                    {
                        pending.Push(childDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add(new ScanWarning("directory-scan-failed", ex.Message, currentDirectory, Certainty.Ambiguous));
            }
        }

        var repoList = repositories.Values.OrderBy(repo => repo.RootPath, StringComparer.OrdinalIgnoreCase).ToArray();
        var normalizedSolutions = solutions
            .DistinctBy(solution => solution.FullPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(solution => solution.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedProjects = projects
            .DistinctBy(project => project.FullPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(project => project.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WorkspaceDiscoveryResult(normalizedRoot, repoList, normalizedSolutions, normalizedProjects, configurationValues, warnings);
    }

    private static DiscoveredRepository? FindOwningRepository(string filePath, IEnumerable<DiscoveredRepository> repositories) =>
        repositories
            .Where(repo => PathUtility.IsUnderPath(filePath, repo.RootPath))
            .OrderByDescending(repo => repo.RootPath.Length)
            .FirstOrDefault();
}
