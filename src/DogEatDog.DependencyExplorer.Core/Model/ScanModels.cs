using System.Text.Json;

namespace DogEatDog.DependencyExplorer.Core.Model;

public enum Certainty
{
    Unresolved = 0,
    Ambiguous = 1,
    Inferred = 2,
    Exact = 3
}

public sealed record SourceLocation(string FilePath, int? Line = null, int? Column = null);

public sealed record ScanWarning(
    string Code,
    string Message,
    string? Path = null,
    Certainty Certainty = Certainty.Unresolved,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public sealed record StageTiming(string Stage, TimeSpan Elapsed);

public sealed record ScanProgressUpdate(
    string Stage,
    string Message,
    int? Current = null,
    int? Total = null);

public sealed record ScanStatistics(
    int RepositoryCount,
    int SolutionCount,
    int ProjectCount,
    int EndpointCount,
    int MethodCount,
    int HttpEdgeCount,
    int TableCount,
    int CrossRepoLinkCount,
    int AmbiguousEdgeCount);

public sealed record ScanMetadata(
    string RootPath,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<StageTiming> Timings,
    IReadOnlyDictionary<string, string> Properties);

public sealed record WorkspaceScanOptions(
    string RootPath,
    int MaxTraversalDepth = 64,
    int MaxPathSearchDepth = 10,
    bool IncludeGeneratedFiles = false,
    bool IncludeAmbiguousEdges = true,
    IReadOnlyList<string>? ExcludedPaths = null)
{
    public static WorkspaceScanOptions Create(string rootPath) =>
        new(PathUtility.NormalizeAbsolutePath(rootPath));
}

public sealed record DiscoveredRepository(string Name, string RootPath);

public sealed record DiscoveredSolution(string Name, string FullPath, string? RepositoryName, string? RepositoryPath);

public sealed record DiscoveredProject(string Name, string FullPath, string? RepositoryName, string? RepositoryPath);

public sealed record DiscoveredConfigurationValue(
    string Key,
    string? Value,
    string FilePath,
    string? RepositoryName,
    string? RepositoryPath);

public sealed record WorkspaceDiscoveryResult(
    string RootPath,
    IReadOnlyList<DiscoveredRepository> Repositories,
    IReadOnlyList<DiscoveredSolution> Solutions,
    IReadOnlyList<DiscoveredProject> Projects,
    IReadOnlyList<DiscoveredConfigurationValue> ConfigurationValues,
    IReadOnlyList<ScanWarning> Warnings);

public static class JsonUtilities
{
    public static IEnumerable<KeyValuePair<string, string?>> FlattenJson(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);
        return FlattenElement(document.RootElement, parentKey: null).ToArray();
    }

    private static IEnumerable<KeyValuePair<string, string?>> FlattenElement(JsonElement element, string? parentKey)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrWhiteSpace(parentKey)
                        ? property.Name
                        : $"{parentKey}:{property.Name}";

                    foreach (var nested in FlattenElement(property.Value, key))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{parentKey}[{index++}]";
                    foreach (var nested in FlattenElement(item, key))
                    {
                        yield return nested;
                    }
                }

                break;

            default:
                yield return new KeyValuePair<string, string?>(parentKey ?? string.Empty, element.ToString());
                break;
        }
    }
}
