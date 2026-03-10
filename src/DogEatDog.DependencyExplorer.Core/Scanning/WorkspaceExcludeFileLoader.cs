using DogEatDog.DependencyExplorer.Core.Model;

namespace DogEatDog.DependencyExplorer.Core.Scanning;

public static class WorkspaceExcludeFileLoader
{
    public static IReadOnlyList<string> LoadExcludedPaths(string rootPath, IReadOnlyList<string>? excludeFilePaths = null)
    {
        var normalizedRoot = PathUtility.NormalizeAbsolutePath(rootPath);
        var files = new List<string>();
        var defaultExcludeFile = Path.Combine(normalizedRoot, ".dedeignore");

        if (File.Exists(defaultExcludeFile))
        {
            files.Add(defaultExcludeFile);
        }

        if (excludeFilePaths is not null)
        {
            foreach (var excludeFilePath in excludeFilePaths)
            {
                if (string.IsNullOrWhiteSpace(excludeFilePath))
                {
                    continue;
                }

                var resolvedPath = Path.GetFullPath(excludeFilePath);
                if (!File.Exists(resolvedPath))
                {
                    throw new FileNotFoundException($"Exclude file '{excludeFilePath}' was not found.", resolvedPath);
                }

                files.Add(resolvedPath);
            }
        }

        var excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var line in File.ReadLines(file))
            {
                var pattern = line.Trim();
                if (string.IsNullOrWhiteSpace(pattern) || pattern.StartsWith('#'))
                {
                    continue;
                }

                excludedPaths.Add(pattern);
            }
        }

        return excludedPaths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
