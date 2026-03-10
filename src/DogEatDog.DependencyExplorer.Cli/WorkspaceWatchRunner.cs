using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Export;
using DogEatDog.DependencyExplorer.Roslyn;

internal sealed class WorkspaceWatchRunner
{
    private readonly DependencyExplorerScanner _scanner;
    private readonly Func<WorkspaceScanOptions> _optionsFactory;
    private readonly string _outputPath;
    private readonly TimeSpan _debounce;

    public WorkspaceWatchRunner(
        DependencyExplorerScanner scanner,
        Func<WorkspaceScanOptions> optionsFactory,
        string outputPath,
        TimeSpan debounce)
    {
        _scanner = scanner;
        _optionsFactory = optionsFactory;
        _outputPath = outputPath;
        _debounce = debounce;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var options = _optionsFactory();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            cts.Cancel();
        };

        await RunScanAsync("initial", cts.Token);

        var signal = NewSignal();
        var sync = new object();
        var reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Trigger(string changeKind, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var snapshot = options;
            if (!ShouldTriggerRescan(path, snapshot))
            {
                return;
            }

            lock (sync)
            {
                reasons.Add($"{changeKind}: {Path.GetRelativePath(snapshot.RootPath, Path.GetFullPath(path))}");
                signal.TrySetResult();
            }
        }

        using var watcher = new FileSystemWatcher(options.RootPath)
        {
            IncludeSubdirectories = true,
            Filter = "*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        FileSystemEventHandler onChanged = (_, args) => Trigger(args.ChangeType.ToString(), args.FullPath);
        RenamedEventHandler onRenamed = (_, args) =>
        {
            Trigger("Renamed", args.OldFullPath);
            Trigger("Renamed", args.FullPath);
        };

        watcher.Changed += onChanged;
        watcher.Created += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += onRenamed;
        watcher.EnableRaisingEvents = true;

        Console.Error.WriteLine($"[watch] Watching {options.RootPath} with debounce {_debounce.TotalMilliseconds:0}ms. Press Ctrl+C to stop.");

        try
        {
            while (!cts.IsCancellationRequested)
            {
                await signal.Task.WaitAsync(cts.Token);
                await Task.Delay(_debounce, cts.Token);

                HashSet<string> batch;
                lock (sync)
                {
                    batch = new HashSet<string>(reasons, StringComparer.OrdinalIgnoreCase);
                    reasons.Clear();
                    signal = NewSignal();
                }

                var reasonSummary = string.Join(", ", batch.Take(5));
                if (batch.Count > 5)
                {
                    reasonSummary += $", +{batch.Count - 5} more";
                }

                await RunScanAsync(reasonSummary, cts.Token);
                options = _optionsFactory();
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[watch] Stopped.");
        }

        return 0;
    }

    private async Task RunScanAsync(string reason, CancellationToken cancellationToken)
    {
        var options = _optionsFactory();
        Console.Error.WriteLine($"[watch] Rescanning because: {reason}");
        var graph = await _scanner.ScanAsync(options.RootPath, options, cancellationToken, new ConsoleScanProgressReporter());
        await GraphJsonExporter.ExportAsync(graph, _outputPath, cancellationToken);
        Console.WriteLine($"Watch update written to {_outputPath} at {DateTimeOffset.UtcNow:O}");
        Console.WriteLine($"Repositories: {graph.Statistics.RepositoryCount}");
        Console.WriteLine($"Projects: {graph.Statistics.ProjectCount}");
        Console.WriteLine($"Endpoints: {graph.Statistics.EndpointCount}");
        Console.WriteLine($"Methods: {graph.Statistics.MethodCount}");
        Console.WriteLine($"HTTP edges: {graph.Statistics.HttpEdgeCount}");
        Console.WriteLine($"Tables: {graph.Statistics.TableCount}");
        Console.WriteLine($"Cross-repo links: {graph.Statistics.CrossRepoLinkCount}");
        Console.WriteLine($"Ambiguous edges: {graph.Statistics.AmbiguousEdgeCount}");
    }

    private static bool ShouldTriggerRescan(string path, WorkspaceScanOptions options)
    {
        var fullPath = Path.GetFullPath(path);
        if (!PathUtility.IsUnderPath(fullPath, options.RootPath))
        {
            return false;
        }

        if (Path.GetFileName(fullPath).Equals(".dedeignore", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (PathUtility.MatchesExcludedPath(fullPath, options.RootPath, options.ExcludedPaths))
        {
            return false;
        }

        var extension = Path.GetExtension(fullPath);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
