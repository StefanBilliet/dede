using DogEatDog.DependencyExplorer.Core.Scanning;

namespace DogEatDog.DependencyExplorer.UnitTests;

public sealed class WorkspaceExcludeFileLoaderTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"dede-ignore-{Guid.NewGuid():N}");

    [Fact]
    public void LoadExcludedPaths_ReadsDefaultAndAdditionalFiles()
    {
        Directory.CreateDirectory(_rootPath);
        File.WriteAllText(Path.Combine(_rootPath, ".dedeignore"), """
            # comment
            tools
            samples
            """);

        var extraFile = Path.Combine(_rootPath, "extra.ignore");
        File.WriteAllText(extraFile, """
            platform/aspnetcore
            samples
            """);

        var excludes = WorkspaceExcludeFileLoader.LoadExcludedPaths(_rootPath, [extraFile]);

        Assert.Equal(
            ["platform/aspnetcore", "samples", "tools"],
            excludes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
