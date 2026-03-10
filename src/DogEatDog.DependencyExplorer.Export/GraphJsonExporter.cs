using System.Text.Json;
using System.Text.Json.Serialization;
using DogEatDog.DependencyExplorer.Graph.Model;

namespace DogEatDog.DependencyExplorer.Export;

public static class GraphJsonExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task ExportAsync(GraphDocument document, string outputPath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
    }

    public static async Task<GraphDocument> LoadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(inputPath);
        var document = await JsonSerializer.DeserializeAsync<GraphDocument>(stream, SerializerOptions, cancellationToken);
        return document ?? throw new InvalidOperationException($"Could not deserialize graph from '{inputPath}'.");
    }
}
