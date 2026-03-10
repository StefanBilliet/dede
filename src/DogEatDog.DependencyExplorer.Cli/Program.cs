using DogEatDog.DependencyExplorer.Export;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Core.Scanning;
using DogEatDog.DependencyExplorer.WebApi;

return await new DedeCli().RunAsync(args);

internal sealed class DedeCli
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "scan" => await ScanAsync(args),
                "watch" => await WatchAsync(args),
                "serve" => await ServeAsync(args),
                "export" => await ExportAsync(args),
                "query" => await QueryAsync(args),
                _ => UnknownCommand()
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> ScanAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dede scan <rootPath> [-o graph.json] [--exclude <path> ...]");
            return 1;
        }

        var rootPath = args[1];
        var output = ReadOption(args, "-o") ?? "graph.json";
        var options = CreateScanOptions(rootPath, args);
        var scanner = DependencyExplorerComposition.CreateScanner();
        var graph = await scanner.ScanAsync(rootPath, options, default, new ConsoleScanProgressReporter());
        await GraphJsonExporter.ExportAsync(graph, output);
        PrintSummary(graph, output);
        return 0;
    }

    private static async Task<int> WatchAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dede watch <rootPath> [-o graph.json] [--debounce-ms 1500] [--exclude-file <path> ...] [--exclude <path> ...]");
            return 1;
        }

        var rootPath = args[1];
        var output = ReadOption(args, "-o") ?? "graph.json";
        var debounceMs = ReadIntOption(args, "--debounce-ms") ?? 1500;
        if (debounceMs < 100)
        {
            Console.Error.WriteLine("The --debounce-ms value must be at least 100.");
            return 1;
        }

        var scanner = DependencyExplorerComposition.CreateScanner();
        var runner = new WorkspaceWatchRunner(
            scanner,
            () => CreateScanOptions(rootPath, args),
            output,
            TimeSpan.FromMilliseconds(debounceMs));

        return await runner.RunAsync();
    }

    private static async Task<int> ServeAsync(string[] args)
    {
        var graphPath = args.Length >= 2 && !args[1].StartsWith("-", StringComparison.Ordinal) ? args[1] : ReadOption(args, "--graph");
        var rootPath = ReadOption(args, "--root");
        var url = ReadOption(args, "--url") ?? "http://127.0.0.1:5057";

        await DependencyExplorerComposition.RunAsync(args, graphPath, rootPath, url);
        return 0;
    }

    private static async Task<int> ExportAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: dede export json <rootPath> -o <file> [--exclude-file <path> ...] [--exclude <path> ...] | dede export neo4j <rootPath> -o <folder> [--exclude-file <path> ...] [--exclude <path> ...]");
            return 1;
        }

        var format = args[1].ToLowerInvariant();
        var rootPath = args[2];
        var output = ReadOption(args, "-o");
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("The -o option is required.");
            return 1;
        }

        var options = CreateScanOptions(rootPath, args);
        var scanner = DependencyExplorerComposition.CreateScanner();
        var graph = await scanner.ScanAsync(rootPath, options, default, new ConsoleScanProgressReporter());

        switch (format)
        {
            case "json":
                await GraphJsonExporter.ExportAsync(graph, output);
                break;
            case "neo4j":
                await Neo4jExportWriter.ExportAsync(graph, output);
                break;
            default:
                Console.Error.WriteLine($"Unsupported export format '{format}'.");
                return 1;
        }

        PrintSummary(graph, output);
        return 0;
    }

    private static async Task<int> QueryAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dede query impact --node <id or name> [--graph graph.json]\n       dede query paths --from <node> --to <node> [--graph graph.json]\n       dede query ambiguous [--graph graph.json] [--limit 50]");
            return 1;
        }

        var subcommand = args[1].ToLowerInvariant();
        var graphPath = ReadOption(args, "--graph") ?? "graph.json";
        var graph = await GraphJsonExporter.LoadAsync(graphPath);
        var engine = new GraphQueryEngine(graph);

        switch (subcommand)
        {
            case "impact":
            {
                var node = ReadOption(args, "--node");
                if (string.IsNullOrWhiteSpace(node))
                {
                    Console.Error.WriteLine("The --node option is required.");
                    return 1;
                }

                var direction = ReadOption(args, "--direction") ?? "downstream";
                var subgraph = engine.GetImpactSubgraph(node, string.Equals(direction, "upstream", StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"Focus: {subgraph.FocusNode.DisplayName} ({subgraph.FocusNode.Type})");
                Console.WriteLine($"Nodes: {subgraph.Nodes.Count}, Edges: {subgraph.Edges.Count}");
                foreach (var edge in subgraph.Edges.Take(40))
                {
                    Console.WriteLine($"{edge.Type,-24} {edge.DisplayName} [{edge.Certainty}]");
                }

                return 0;
            }

            case "paths":
            {
                var from = ReadOption(args, "--from");
                var to = ReadOption(args, "--to");
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    Console.Error.WriteLine("The --from and --to options are required.");
                    return 1;
                }

                var result = engine.FindPaths(from, to);
                Console.WriteLine($"Paths from {result.From.DisplayName} to {result.To.DisplayName}: {result.Paths.Count}");
                foreach (var path in result.Paths.Take(10))
                {
                    Console.WriteLine(string.Join(" -> ", path.Nodes.Select(node => node.DisplayName)));
                }

                return 0;
            }

            case "ambiguous":
            {
                var limit = ReadIntOption(args, "--limit") ?? 50;
                var review = engine.GetAmbiguousReview();

                Console.WriteLine($"=== Ambiguous edges requiring human review ===");
                Console.WriteLine($"  Unresolved edges : {review.UnresolvedEdges.Count}");
                Console.WriteLine($"  Ambiguous edges  : {review.AmbiguousEdges.Count}");
                Console.WriteLine($"  Unresolved symbols: {review.UnresolvedSymbols.Count}");
                Console.WriteLine();

                if (review.UnresolvedEdges.Count > 0)
                {
                    Console.WriteLine($"--- Unresolved edges (certainty=0) [showing up to {limit}] ---");
                    foreach (var edge in review.UnresolvedEdges.Take(limit))
                    {
                        var loc = edge.SourceLocation is null ? string.Empty : $" @ {edge.SourceLocation.FilePath}:{edge.SourceLocation.Line}";
                        Console.WriteLine($"  [{edge.Type,-24}] {edge.DisplayName}{loc}");
                    }
                    if (review.UnresolvedEdges.Count > limit)
                    {
                        Console.WriteLine($"  ... and {review.UnresolvedEdges.Count - limit} more.");
                    }
                    Console.WriteLine();
                }

                if (review.AmbiguousEdges.Count > 0)
                {
                    Console.WriteLine($"--- Ambiguous edges (certainty=1) [showing up to {limit}] ---");
                    var byType = review.AmbiguousEdges
                        .GroupBy(edge => edge.Type)
                        .OrderBy(g => g.Key.ToString());
                    foreach (var typeGroup in byType)
                    {
                        Console.WriteLine($"  {typeGroup.Key} ({typeGroup.Count()})");
                    }
                    Console.WriteLine();
                    foreach (var edge in review.AmbiguousEdges.Take(limit))
                    {
                        var loc = edge.SourceLocation is null ? string.Empty : $" @ {edge.SourceLocation.FilePath}:{edge.SourceLocation.Line}";
                        Console.WriteLine($"  [{edge.Type,-24}] {edge.DisplayName}{loc}");
                    }
                    if (review.AmbiguousEdges.Count > limit)
                    {
                        Console.WriteLine($"  ... and {review.AmbiguousEdges.Count - limit} more. Use --limit <n> to see more.");
                    }
                    Console.WriteLine();
                }

                if (review.UnresolvedSymbols.Count > 0)
                {
                    Console.WriteLine($"--- Unresolved symbols [showing up to {limit}] ---");
                    foreach (var warning in review.UnresolvedSymbols.Take(limit))
                    {
                        var path = warning.Path is null ? string.Empty : $" ({warning.Path})";
                        Console.WriteLine($"  [{warning.Code}] {warning.Message}{path}");
                    }
                    if (review.UnresolvedSymbols.Count > limit)
                    {
                        Console.WriteLine($"  ... and {review.UnresolvedSymbols.Count - limit} more. Use --limit <n> to see more.");
                    }
                }

                return 0;
            }

            default:
                Console.Error.WriteLine($"Unsupported query subcommand '{subcommand}'.");
                return 1;
        }
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadOptions(IReadOnlyList<string> args, string name)
    {
        var values = new List<string>();
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                values.Add(args[index + 1]);
            }
        }

        return values;
    }

    private static int? ReadIntOption(IReadOnlyList<string> args, string name)
    {
        var value = ReadOption(args, name);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Option '{name}' requires an integer value.");
    }

    private static WorkspaceScanOptions CreateScanOptions(string rootPath, IReadOnlyList<string> args)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var excludedPaths = ReadOptions(args, "--exclude");
        var excludeFilePaths = ReadOptions(args, "--exclude-file");
        var fileExcludedPaths = WorkspaceExcludeFileLoader.LoadExcludedPaths(normalizedRoot, excludeFilePaths);
        var combinedExcludedPaths = excludedPaths
            .Concat(fileExcludedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WorkspaceScanOptions(
            normalizedRoot,
            ExcludedPaths: combinedExcludedPaths);
    }

    private static void PrintSummary(DogEatDog.DependencyExplorer.Graph.Model.GraphDocument graph, string output)
    {
        Console.WriteLine($"Graph written to {output}");
        Console.WriteLine($"Repositories: {graph.Statistics.RepositoryCount}");
        Console.WriteLine($"Projects: {graph.Statistics.ProjectCount}");
        Console.WriteLine($"Endpoints: {graph.Statistics.EndpointCount}");
        Console.WriteLine($"Methods: {graph.Statistics.MethodCount}");
        Console.WriteLine($"HTTP edges: {graph.Statistics.HttpEdgeCount}");
        Console.WriteLine($"Tables: {graph.Statistics.TableCount}");
        Console.WriteLine($"Cross-repo links: {graph.Statistics.CrossRepoLinkCount}");
        Console.WriteLine($"Ambiguous edges: {graph.Statistics.AmbiguousEdgeCount}");
    }

    private static int UnknownCommand()
    {
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("dede commands:");
        Console.WriteLine("  dede scan <rootPath> [-o graph.json] [--exclude-file <path> ...] [--exclude <path> ...]");
        Console.WriteLine("  dede watch <rootPath> [-o graph.json] [--debounce-ms 1500] [--exclude-file <path> ...] [--exclude <path> ...]");
        Console.WriteLine("  dede serve <graphJsonPath> [--url http://127.0.0.1:5057]");
        Console.WriteLine("  dede export json <rootPath> -o <file> [--exclude-file <path> ...] [--exclude <path> ...]");
        Console.WriteLine("  dede export neo4j <rootPath> -o <folder> [--exclude-file <path> ...] [--exclude <path> ...]");
        Console.WriteLine("  dede query impact --node <id or name> [--graph graph.json]");
        Console.WriteLine("  dede query paths --from <node> --to <node> [--graph graph.json]");
        Console.WriteLine("  dede query ambiguous [--graph graph.json] [--limit 50]");
    }
}

internal sealed class ConsoleScanProgressReporter : IProgress<ScanProgressUpdate>
{
    private const int MaxFailureLinesPerSignature = 3;
    private readonly Dictionary<string, int> _failureCounts = new(StringComparer.Ordinal);

    public void Report(ScanProgressUpdate value)
    {
        if (TryHandleRepeatedFailure(value))
        {
            return;
        }

        var prefix = value.Current.HasValue && value.Total.HasValue
            ? $"[{value.Stage} {value.Current}/{value.Total}]"
            : $"[{value.Stage}]";

        Console.Error.WriteLine($"{prefix} {value.Message}");

        if (string.Equals(value.Stage, "scan", StringComparison.Ordinal) && value.Message.StartsWith("Scan complete", StringComparison.Ordinal))
        {
            PrintFailureSummary();
        }
    }

    private bool TryHandleRepeatedFailure(ScanProgressUpdate value)
    {
        if (!value.Message.StartsWith("Failed to ", StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = value.Message.IndexOf(": ", StringComparison.Ordinal);
        var signature = separatorIndex >= 0
            ? value.Message[(separatorIndex + 2)..]
            : value.Message;

        _failureCounts.TryGetValue(signature, out var count);
        count++;
        _failureCounts[signature] = count;

        if (count <= MaxFailureLinesPerSignature)
        {
            return false;
        }

        if (count == MaxFailureLinesPerSignature + 1)
        {
            var prefix = value.Current.HasValue && value.Total.HasValue
                ? $"[{value.Stage} {value.Current}/{value.Total}]"
                : $"[{value.Stage}]";

            Console.Error.WriteLine($"{prefix} Suppressing additional repeated failures for: {signature}");
        }

        return true;
    }

    private void PrintFailureSummary()
    {
        foreach (var failure in _failureCounts
                     .Where(pair => pair.Value > MaxFailureLinesPerSignature)
                     .OrderByDescending(pair => pair.Value))
        {
            Console.Error.WriteLine($"[warnings] Suppressed {failure.Value - MaxFailureLinesPerSignature} repeated failures: {failure.Key}");
        }
    }
}
