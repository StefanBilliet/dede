using DogEatDog.DependencyExplorer.Core.Model;
using Microsoft.CodeAnalysis;

namespace DogEatDog.DependencyExplorer.Roslyn;

public interface IRoslynGraphContributor
{
    string Name { get; }

    Task ContributeAsync(RoslynProjectContext projectContext, Graph.GraphBuilder graphBuilder, CancellationToken cancellationToken);
}

public sealed record TypeReference(
    string Id,
    string DisplayName,
    SourceLocation? SourceLocation,
    string? RepositoryName,
    string? ProjectName);

public sealed record MethodReference(
    string Id,
    string ContainingTypeId,
    string DisplayName,
    SourceLocation? SourceLocation,
    string? RepositoryName,
    string? ProjectName);

public sealed record ServiceRegistration(
    string InterfaceId,
    string ImplementationId,
    string RepositoryName,
    string ProjectName,
    SourceLocation? SourceLocation);

public sealed class WorkspaceSymbolCatalog
{
    public Dictionary<string, List<TypeReference>> ImplementationsByInterfaceId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<MethodReference>> ImplementationMethodsByInterfaceMethodId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<ServiceRegistration>> RegisteredImplementationsByInterfaceId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public MediatRSymbolCatalog MediatR { get; } = new();
}

public sealed class MediatRSymbolCatalog
{
    public Dictionary<string, List<MethodReference>> RequestHandlerMethodsByRequestTypeId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<MethodReference>> NotificationHandlerMethodsByNotificationTypeId { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RoslynWorkspaceContext
{
    private readonly Dictionary<ProjectId, RoslynProjectContext> _projectsById;

    public RoslynWorkspaceContext(
        WorkspaceScanOptions options,
        WorkspaceDiscoveryResult discovery,
        IReadOnlyList<RoslynProjectContext> projects,
        WorkspaceSymbolCatalog symbolCatalog,
        IReadOnlyList<ScanWarning> warnings)
    {
        Options = options;
        Discovery = discovery;
        Projects = projects;
        SymbolCatalog = symbolCatalog;
        Warnings = warnings;
        _projectsById = projects.ToDictionary(project => project.Project.Id);
    }

    public WorkspaceScanOptions Options { get; }

    public WorkspaceDiscoveryResult Discovery { get; }

    public IReadOnlyList<RoslynProjectContext> Projects { get; }

    public WorkspaceSymbolCatalog SymbolCatalog { get; }

    public IReadOnlyList<ScanWarning> Warnings { get; }

    public RoslynProjectContext? FindProject(ProjectId id) => _projectsById.GetValueOrDefault(id);
}

public sealed class RoslynProjectContext
{
    public RoslynProjectContext(
        RoslynWorkspaceContextAccessor accessor,
        Project project,
        Compilation compilation,
        string? solutionPath,
        string? repositoryName,
        string? repositoryPath)
    {
        Accessor = accessor;
        Project = project;
        Compilation = compilation;
        SolutionPath = solutionPath;
        RepositoryName = repositoryName;
        RepositoryPath = repositoryPath;
    }

    internal RoslynWorkspaceContextAccessor Accessor { get; }

    public RoslynWorkspaceContext Workspace => Accessor.Value;

    public Project Project { get; }

    public Compilation Compilation { get; }

    public string? SolutionPath { get; }

    public string? RepositoryName { get; }

    public string? RepositoryPath { get; }

    public string ProjectName => Project.Name;

    public IEnumerable<DiscoveredConfigurationValue> ConfigurationValues =>
        Workspace.Discovery.ConfigurationValues.Where(value =>
            string.Equals(value.RepositoryPath, RepositoryPath, StringComparison.OrdinalIgnoreCase));
}

public sealed class RoslynWorkspaceContextAccessor
{
    public RoslynWorkspaceContext Value { get; set; } = null!;
}
