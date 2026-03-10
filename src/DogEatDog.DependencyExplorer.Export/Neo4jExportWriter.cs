using System.Text;
using DogEatDog.DependencyExplorer.Graph.Model;

namespace DogEatDog.DependencyExplorer.Export;

public static class Neo4jExportWriter
{
    public static async Task ExportAsync(GraphDocument document, string outputFolder, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputFolder);
        var nodesPath = Path.Combine(outputFolder, "nodes.csv");
        var edgesPath = Path.Combine(outputFolder, "edges.csv");

        await File.WriteAllTextAsync(nodesPath, BuildNodesCsv(document), cancellationToken);
        await File.WriteAllTextAsync(edgesPath, BuildEdgesCsv(document), cancellationToken);
    }

    private static string BuildNodesCsv(GraphDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("id,type,displayName,repositoryName,projectName,certainty");
        foreach (var node in document.Nodes)
        {
            builder.AppendLine(string.Join(',',
                Escape(node.Id),
                Escape(node.Type.ToString()),
                Escape(node.DisplayName),
                Escape(node.RepositoryName),
                Escape(node.ProjectName),
                Escape(node.Certainty.ToString())));
        }

        return builder.ToString();
    }

    private static string BuildEdgesCsv(GraphDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("id,sourceId,targetId,type,displayName,repositoryName,projectName,certainty");
        foreach (var edge in document.Edges)
        {
            builder.AppendLine(string.Join(',',
                Escape(edge.Id),
                Escape(edge.SourceId),
                Escape(edge.TargetId),
                Escape(edge.Type.ToString()),
                Escape(edge.DisplayName),
                Escape(edge.RepositoryName),
                Escape(edge.ProjectName),
                Escape(edge.Certainty.ToString())));
        }

        return builder.ToString();
    }

    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
