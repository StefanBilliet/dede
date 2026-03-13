using DogEatDog.DependencyExplorer.AspNet;
using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Core.Scanning;
using DogEatDog.DependencyExplorer.EFCore;
using DogEatDog.DependencyExplorer.Export;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using DogEatDog.DependencyExplorer.HttpDiscovery;
using DogEatDog.DependencyExplorer.Roslyn;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;

namespace DogEatDog.DependencyExplorer.WebApi;

public static class DependencyExplorerComposition
{
    public static DependencyExplorerScanner CreateScanner() =>
        new(
            new FileSystemWorkspaceDiscoverer(),
            new RoslynWorkspaceLoader(),
            [new AspNetGraphContributor(), new HttpGraphContributor(), new EfCoreGraphContributor()]);

    public static async Task RunAsync(
        string[] args,
        string? graphPath = null,
        string? rootPath = null,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = null;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton(CreateScanner());
        builder.Services.AddSingleton<GraphState>();
        builder.Services.AddSingleton<DemoPresetProvider>();

        if (!string.IsNullOrWhiteSpace(url))
        {
            builder.WebHost.UseUrls(url);
        }

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStaticFiles();
        ConfigureWebUiStaticFiles(app);

        app.MapGet("/", () => Results.Redirect("/_content/DogEatDog.DependencyExplorer.WebUi/index.html"));
        app.MapGet("/react", () => Results.Redirect("/_content/DogEatDog.DependencyExplorer.WebUi/react/index.html"));
        app.MapGet("/api/graph", (GraphState state) =>
            state.Document is null
                ? Results.NotFound(new { message = "No graph is loaded. Use /api/scan or start with a graph path." })
                : Results.Ok(state.Document));

        app.MapGet("/api/summary", (GraphState state) =>
            state.Document is null
                ? Results.NotFound(new { message = "No graph is loaded." })
                : Results.Ok(new { state.Document.Statistics, state.Document.ScanMetadata, WarningCount = state.Document.Warnings.Count }));

        app.MapPost("/api/scan", async (ScanRequest request, DependencyExplorerScanner scanner, GraphState state, CancellationToken ct) =>
        {
            var targetRoot = string.IsNullOrWhiteSpace(request.RootPath) ? rootPath : request.RootPath;
            if (string.IsNullOrWhiteSpace(targetRoot))
            {
                return Results.BadRequest(new { message = "A rootPath is required." });
            }

            var graph = await scanner.ScanAsync(targetRoot!, WorkspaceScanOptions.Create(targetRoot!), ct);
            state.Document = graph;
            state.GraphPath = null;
            return Results.Ok(graph);
        });

        app.MapGet("/api/query/impact", (string node, string? direction, int? depth, bool? exactOnly, bool? includeAmbiguous, GraphState state) =>
        {
            if (state.Document is null)
            {
                return Results.NotFound(new { message = "No graph is loaded." });
            }

            var engine = new GraphQueryEngine(state.Document);
            var subgraph = engine.GetImpactSubgraph(
                node,
                upstream: string.Equals(direction, "upstream", StringComparison.OrdinalIgnoreCase),
                maxDepth: depth ?? 6,
                exactOnly: exactOnly ?? false,
                includeAmbiguous: includeAmbiguous ?? true);

            return Results.Ok(subgraph);
        });

        app.MapGet("/api/query/paths", (string from, string to, int? depth, bool? exactOnly, bool? includeAmbiguous, GraphState state) =>
        {
            if (state.Document is null)
            {
                return Results.NotFound(new { message = "No graph is loaded." });
            }

            var engine = new GraphQueryEngine(state.Document);
            return Results.Ok(engine.FindPaths(from, to, depth ?? 8, exactOnly ?? false, includeAmbiguous ?? false));
        });

        app.MapGet("/api/presets", (DemoPresetProvider presets) => Results.Ok(presets.GetPresets()));

        if (!string.IsNullOrWhiteSpace(graphPath) && File.Exists(graphPath))
        {
            var state = app.Services.GetRequiredService<GraphState>();
            state.Document = await GraphJsonExporter.LoadAsync(graphPath, cancellationToken);
            state.GraphPath = graphPath;
        }
        else if (!string.IsNullOrWhiteSpace(rootPath))
        {
            var scanner = app.Services.GetRequiredService<DependencyExplorerScanner>();
            var state = app.Services.GetRequiredService<GraphState>();
            state.Document = await scanner.ScanAsync(rootPath, WorkspaceScanOptions.Create(rootPath), cancellationToken);
        }

        await app.RunAsync(cancellationToken);
    }

    private static void ConfigureWebUiStaticFiles(WebApplication app)
    {
        var webUiRoot = ResolveWebUiRoot(app.Environment.ContentRootPath);
        if (webUiRoot is null)
        {
            return;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(webUiRoot),
            RequestPath = "/_content/DogEatDog.DependencyExplorer.WebUi"
        });
    }

    private static string? ResolveWebUiRoot(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, "src", "DogEatDog.DependencyExplorer.WebUi", "wwwroot"),
            Path.Combine(contentRootPath, "..", "DogEatDog.DependencyExplorer.WebUi", "wwwroot"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DogEatDog.DependencyExplorer.WebUi", "wwwroot")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}

public sealed class GraphState
{
    public GraphDocument? Document { get; set; }

    public string? GraphPath { get; set; }
}

public sealed record ScanRequest(string RootPath);
