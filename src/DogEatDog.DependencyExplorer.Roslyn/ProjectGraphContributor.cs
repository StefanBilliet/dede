using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal sealed class ProjectGraphContributor : IRoslynGraphContributor
{
    public string Name => nameof(ProjectGraphContributor);

    public Task ContributeAsync(RoslynProjectContext projectContext, GraphBuilder graphBuilder, CancellationToken cancellationToken)
    {
        var projectId = DependencyExplorerScanner.CreateProjectNodeId(projectContext.Project.FilePath ?? projectContext.ProjectName);
        var solutionId = projectContext.SolutionPath is null ? null : DependencyExplorerScanner.CreateSolutionNodeId(projectContext.SolutionPath);

        graphBuilder.AddNode(
            projectId,
            GraphNodeType.Project,
            projectContext.Project.Name,
            repositoryName: projectContext.RepositoryName,
            projectName: projectContext.ProjectName,
            certainty: Certainty.Exact,
            metadata: new Dictionary<string, string?>
            {
                ["filePath"] = projectContext.Project.FilePath,
                ["assemblyName"] = projectContext.Compilation.AssemblyName,
                ["solutionPath"] = projectContext.SolutionPath
            });

        if (solutionId is not null)
        {
            graphBuilder.AddEdge(
                solutionId,
                projectId,
                GraphEdgeType.CONTAINS,
                $"{Path.GetFileNameWithoutExtension(projectContext.SolutionPath)} contains {projectContext.ProjectName}",
                repositoryName: projectContext.RepositoryName,
                projectName: projectContext.ProjectName);
        }

        foreach (var projectReference in projectContext.Project.ProjectReferences)
        {
            var referencedProject = projectContext.Workspace.FindProject(projectReference.ProjectId);
            if (referencedProject is null)
            {
                continue;
            }

            var referencedId = DependencyExplorerScanner.CreateProjectNodeId(referencedProject.Project.FilePath ?? referencedProject.ProjectName);
            graphBuilder.AddEdge(
                projectId,
                referencedId,
                GraphEdgeType.DEPENDS_ON,
                $"{projectContext.ProjectName} references {referencedProject.ProjectName}",
                repositoryName: projectContext.RepositoryName,
                projectName: projectContext.ProjectName);

            if (!string.Equals(projectContext.RepositoryName, referencedProject.RepositoryName, StringComparison.OrdinalIgnoreCase))
            {
                graphBuilder.AddEdge(
                    projectId,
                    referencedId,
                    GraphEdgeType.CROSSES_REPO_BOUNDARY,
                    $"{projectContext.ProjectName} crosses into {referencedProject.ProjectName}",
                    repositoryName: projectContext.RepositoryName,
                    projectName: projectContext.ProjectName);
            }
        }

        foreach (var syntaxTree in projectContext.Compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!projectContext.Workspace.Options.IncludeGeneratedFiles && SymbolUtilities.IsGeneratedFile(syntaxTree.FilePath))
            {
                continue;
            }

            var semanticModel = projectContext.Compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot(cancellationToken);

            foreach (var typeSyntax in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeSyntax, cancellationToken) is not INamedTypeSymbol typeSymbol)
                {
                    continue;
                }

                AddTypeNode(projectContext, graphBuilder, projectId, typeSymbol, typeSyntax);
                AddInjectionEdges(projectContext, graphBuilder, typeSymbol);
            }

            foreach (var methodSyntax in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                AddMethodGraph(projectContext, graphBuilder, projectId, semanticModel, methodSyntax, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    private static void AddTypeNode(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        string projectId,
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax typeSyntax)
    {
        var nodeType = SymbolUtilities.ClassifyType(typeSymbol);
        var typeId = SymbolUtilities.CreateTypeId(typeSymbol);
        graphBuilder.AddNode(
            typeId,
            nodeType,
            SymbolUtilities.GetTypeDisplayName(typeSymbol),
            SymbolUtilities.ToSourceLocation(typeSymbol),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact,
            new Dictionary<string, string?>
            {
                ["fullName"] = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ["namespace"] = typeSymbol.ContainingNamespace?.ToDisplayString(),
                ["kind"] = typeSymbol.TypeKind.ToString()
            });

        graphBuilder.AddEdge(
            projectId,
            typeId,
            GraphEdgeType.DEFINES,
            $"{projectContext.ProjectName} defines {typeSymbol.Name}",
            SymbolUtilities.ToSourceLocation(typeSyntax),
            projectContext.RepositoryName,
            projectContext.ProjectName);

        foreach (var interfaceSymbol in typeSymbol.Interfaces)
        {
            var interfaceId = SymbolUtilities.CreateTypeId(interfaceSymbol);
            graphBuilder.AddNode(
                interfaceId,
                GraphNodeType.Interface,
                SymbolUtilities.GetTypeDisplayName(interfaceSymbol),
                SymbolUtilities.ToSourceLocation(interfaceSymbol),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact,
                new Dictionary<string, string?> { ["fullName"] = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) });

            graphBuilder.AddEdge(
                typeId,
                interfaceId,
                GraphEdgeType.IMPLEMENTS,
                $"{typeSymbol.Name} implements {interfaceSymbol.Name}",
                SymbolUtilities.ToSourceLocation(typeSyntax),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);
        }
    }

    private static void AddInjectionEdges(RoslynProjectContext projectContext, GraphBuilder graphBuilder, INamedTypeSymbol typeSymbol)
    {
        var sourceTypeId = SymbolUtilities.CreateTypeId(typeSymbol);
        foreach (var constructor in typeSymbol.InstanceConstructors.Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public))
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (parameter.Type is not INamedTypeSymbol parameterType)
                {
                    continue;
                }

                if (!SymbolUtilities.IsSourceSymbol(parameterType))
                {
                    continue;
                }

                var parameterTypeId = SymbolUtilities.CreateTypeId(parameterType);
                graphBuilder.AddNode(
                    parameterTypeId,
                    SymbolUtilities.ClassifyType(parameterType),
                    SymbolUtilities.GetTypeDisplayName(parameterType),
                    SymbolUtilities.ToSourceLocation(parameterType),
                    projectContext.RepositoryName,
                    projectContext.ProjectName,
                    Certainty.Exact);

                graphBuilder.AddEdge(
                    sourceTypeId,
                    parameterTypeId,
                    GraphEdgeType.INJECTS,
                    $"{typeSymbol.Name} injects {parameterType.Name}",
                    SymbolUtilities.ToSourceLocation(constructor),
                    projectContext.RepositoryName,
                    projectContext.ProjectName,
                    Certainty.Exact);
            }
        }
    }

    private static void AddMethodGraph(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        string projectId,
        SemanticModel semanticModel,
        BaseMethodDeclarationSyntax methodSyntax,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (methodSymbol.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove)
        {
            return;
        }

        var containingType = methodSymbol.ContainingType;
        var typeId = SymbolUtilities.CreateTypeId(containingType);
        var methodId = SymbolUtilities.CreateMethodId(methodSymbol);

        graphBuilder.AddNode(
            methodId,
            GraphNodeType.Method,
            SymbolUtilities.GetMethodDisplayName(methodSymbol),
            SymbolUtilities.ToSourceLocation(methodSymbol),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact,
            new Dictionary<string, string?>
            {
                ["fullName"] = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ["returns"] = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            });

        graphBuilder.AddEdge(
            typeId,
            methodId,
            GraphEdgeType.DEFINES,
            $"{containingType.Name} defines {methodSymbol.Name}",
            SymbolUtilities.ToSourceLocation(methodSyntax),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact);

        if (methodSyntax.Body is null && methodSyntax.ExpressionBody is null)
        {
            return;
        }

        foreach (var invocation in methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (symbol is null)
            {
                continue;
            }

            AddCallEdge(projectContext, graphBuilder, semanticModel, methodSymbol, methodId, symbol, invocation, cancellationToken);
        }
    }

    private static void AddCallEdge(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        IMethodSymbol caller,
        string callerId,
        IMethodSymbol targetMethod,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        targetMethod = (IMethodSymbol)targetMethod.OriginalDefinition;
        var targetLocation = SymbolUtilities.ToSourceLocation(targetMethod) ?? SymbolUtilities.ToSourceLocation(invocation);

        if (SymbolUtilities.IsSourceSymbol(targetMethod))
        {
            var targetId = SymbolUtilities.CreateMethodId(targetMethod);
            graphBuilder.AddNode(
                targetId,
                GraphNodeType.Method,
                SymbolUtilities.GetMethodDisplayName(targetMethod),
                targetLocation,
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);

            graphBuilder.AddEdge(
                callerId,
                targetId,
                GraphEdgeType.CALLS,
                $"{caller.ContainingType.Name}.{caller.Name} calls {targetMethod.ContainingType.Name}.{targetMethod.Name}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);

            if (targetMethod.ContainingType.TypeKind != TypeKind.Interface)
            {
                return;
            }
        }

        AddMediatRDispatchEdges(projectContext, graphBuilder, semanticModel, caller, callerId, targetMethod, invocation, cancellationToken);

        if (targetMethod.ContainingType.TypeKind != TypeKind.Interface)
        {
            return;
        }

        var interfaceMethodId = SymbolUtilities.CreateMethodId(targetMethod);
        var registered = projectContext.Workspace.SymbolCatalog.RegisteredImplementationsByInterfaceId
            .GetValueOrDefault(SymbolUtilities.CreateTypeId((INamedTypeSymbol)targetMethod.ContainingType), []);

        var implementationMethods = registered.Count > 0
            ? projectContext.Workspace.SymbolCatalog.ImplementationMethodsByInterfaceMethodId
                .GetValueOrDefault(interfaceMethodId, [])
                .Where(method => registered.Any(reg => string.Equals(reg.ImplementationId, method.ContainingTypeId, StringComparison.OrdinalIgnoreCase)))
                .ToArray()
            : projectContext.Workspace.SymbolCatalog.ImplementationMethodsByInterfaceMethodId
                .GetValueOrDefault(interfaceMethodId, [])
                .ToArray();

        if (implementationMethods.Length == 0)
        {
            return;
        }

        var certainty = implementationMethods.Length == 1 ? Certainty.Inferred : Certainty.Ambiguous;
        foreach (var implementationMethod in implementationMethods)
        {
            graphBuilder.AddNode(
                implementationMethod.Id,
                GraphNodeType.Method,
                implementationMethod.DisplayName,
                implementationMethod.SourceLocation,
                implementationMethod.RepositoryName,
                implementationMethod.ProjectName,
                certainty);

            graphBuilder.AddEdge(
                callerId,
                implementationMethod.Id,
                certainty == Certainty.Ambiguous ? GraphEdgeType.AMBIGUOUS : GraphEdgeType.CALLS,
                $"{caller.ContainingType.Name}.{caller.Name} resolves {targetMethod.Name}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                certainty,
                new Dictionary<string, string?>
                {
                    ["interfaceMethod"] = interfaceMethodId,
                    ["resolution"] = registered.Count > 0 ? "service-registration" : "interface-implementation"
                });
        }
    }

    private static void AddMediatRDispatchEdges(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        IMethodSymbol caller,
        string callerId,
        IMethodSymbol targetMethod,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var resolution = MediatRSendDispatchResolver.TryResolve(
            invocation,
            semanticModel,
            targetMethod,
            projectContext.Workspace.SymbolCatalog,
            cancellationToken);

        if (resolution is null)
        {
            return;
        }

        graphBuilder.AddNode(
            resolution.RequestTypeId,
            SymbolUtilities.ClassifyType(resolution.RequestTypeSymbol),
            SymbolUtilities.GetTypeDisplayName(resolution.RequestTypeSymbol),
            SymbolUtilities.ToSourceLocation(resolution.RequestTypeSymbol),
            certainty: Certainty.Exact,
            metadata: new Dictionary<string, string?>
            {
                ["fullName"] = resolution.RequestTypeDisplayName
            });

        graphBuilder.AddEdge(
            callerId,
            resolution.RequestTypeId,
            GraphEdgeType.DISPATCHES,
            $"{caller.ContainingType.Name}.{caller.Name} dispatches {resolution.RequestTypeSymbol.Name}",
            SymbolUtilities.ToSourceLocation(invocation),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact,
            CreateMediatRMetadata(
                resolution,
                includeMediatorMethod: true,
                includeResolution: false));

        var certainty = resolution.DetermineCertainty();
        foreach (var handlerMethod in resolution.HandlerMethods.DistinctBy(method => method.Id, StringComparer.OrdinalIgnoreCase))
        {
            graphBuilder.AddNode(
                handlerMethod.Id,
                GraphNodeType.Method,
                handlerMethod.DisplayName,
                handlerMethod.SourceLocation,
                handlerMethod.RepositoryName,
                handlerMethod.ProjectName,
                certainty);

            graphBuilder.AddEdge(
                resolution.RequestTypeId,
                handlerMethod.Id,
                GraphEdgeType.HANDLED_BY,
                $"{resolution.RequestTypeSymbol.Name} handled by {handlerMethod.DisplayName}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                certainty,
                CreateMediatRMetadata(
                    resolution,
                    includeMediatorMethod: false,
                    includeResolution: true));
        }
    }
    
    private static Dictionary<string, string?> CreateMediatRMetadata(
        MediatRSendDispatchResolution resolution,
        bool includeMediatorMethod,
        bool includeResolution)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["dispatchFramework"] = "MediatR",
            ["dispatchKind"] = resolution.DispatchKind,
            ["requestType"] = resolution.RequestTypeDisplayName
        };

        if (includeMediatorMethod)
        {
            metadata["mediatorMethod"] = resolution.MediatorMethodDisplayName;
        }

        if (includeResolution)
        {
            metadata["resolution"] = "request-handler";
        }

        return metadata;
    }
}
