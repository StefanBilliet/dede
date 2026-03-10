namespace DogEatDog.DependencyExplorer.Core.Model;

public static class PathUtility
{
    public static string NormalizeAbsolutePath(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static bool IsUnderPath(string candidatePath, string rootPath)
    {
        var candidate = NormalizeAbsolutePath(candidatePath);
        var root = NormalizeAbsolutePath(rootPath);
        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesExcludedPath(string candidatePath, string rootPath, IReadOnlyList<string>? excludedPaths)
    {
        if (excludedPaths is null || excludedPaths.Count == 0)
        {
            return false;
        }

        var candidate = NormalizeAbsolutePath(candidatePath);
        var normalizedRoot = NormalizeAbsolutePath(rootPath);
        var candidateNormalizedSeparators = NormalizeSeparators(candidate);
        var relativeToRoot = NormalizeSeparators(Path.GetRelativePath(normalizedRoot, candidate));

        foreach (var rawPattern in excludedPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPattern))
            {
                continue;
            }

            var pattern = rawPattern.Trim();
            if (Path.IsPathRooted(pattern))
            {
                if (IsUnderPath(candidate, pattern))
                {
                    return true;
                }

                continue;
            }

            var normalizedPattern = NormalizeSeparators(pattern).Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                continue;
            }

            if (relativeToRoot.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase)
                || relativeToRoot.StartsWith(normalizedPattern + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (candidateNormalizedSeparators.Contains("/" + normalizedPattern + "/", StringComparison.OrdinalIgnoreCase)
                || candidateNormalizedSeparators.EndsWith("/" + normalizedPattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string ToRepoRelativePath(string rootPath, string fullPath) =>
        Path.GetRelativePath(NormalizeAbsolutePath(rootPath), NormalizeAbsolutePath(fullPath))
            .Replace('\\', '/');

    public static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');
}
