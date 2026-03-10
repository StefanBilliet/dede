using System.Text.RegularExpressions;
using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using DogEatDog.DependencyExplorer.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.EFCore;

public sealed partial class EfCoreGraphContributor : IRoslynGraphContributor
{
    public string Name => nameof(EfCoreGraphContributor);

    public Task ContributeAsync(RoslynProjectContext projectContext, GraphBuilder graphBuilder, CancellationToken cancellationToken)
    {
        var entityTableMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var syntaxTree in projectContext.Compilation.SyntaxTrees)
        {
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

                if (SymbolUtilities.InheritsFrom(typeSymbol, "Microsoft.EntityFrameworkCore.DbContext"))
                {
                    AddDbContextNode(projectContext, graphBuilder, typeSymbol);
                    RegisterDbSetEntities(projectContext, graphBuilder, typeSymbol, entityTableMappings);
                    RegisterToTableMappings(projectContext, graphBuilder, semanticModel, typeSyntax, entityTableMappings);
                }
                else if (TryGetTableFromAttributes(typeSymbol) is { } tableName)
                {
                    AddEntityTableMapping(projectContext, graphBuilder, typeSymbol, tableName, Certainty.Exact);
                    entityTableMappings[SymbolUtilities.CreateTypeId(typeSymbol)] = tableName;
                }
            }

            foreach (var methodSyntax in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                AnalyzeMethod(projectContext, graphBuilder, semanticModel, methodSyntax, entityTableMappings, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    private static void AddDbContextNode(RoslynProjectContext projectContext, GraphBuilder graphBuilder, INamedTypeSymbol dbContextSymbol)
    {
        var dbContextId = SymbolUtilities.CreateTypeId(dbContextSymbol);
        graphBuilder.AddNode(
            dbContextId,
            GraphNodeType.DbContext,
            dbContextSymbol.Name,
            SymbolUtilities.ToSourceLocation(dbContextSymbol),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact,
            new Dictionary<string, string?> { ["fullName"] = dbContextSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) });
    }

    private static void RegisterDbSetEntities(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        INamedTypeSymbol dbContextSymbol,
        Dictionary<string, string> entityTableMappings)
    {
        foreach (var property in dbContextSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.Type is not INamedTypeSymbol propertyType
                || !string.Equals(propertyType.Name, "DbSet", StringComparison.Ordinal)
                || propertyType.TypeArguments.Length != 1
                || propertyType.TypeArguments[0] is not INamedTypeSymbol entitySymbol)
            {
                continue;
            }

            var inferredTable = entityTableMappings.GetValueOrDefault(SymbolUtilities.CreateTypeId(entitySymbol))
                ?? TryGetTableFromAttributes(entitySymbol)
                ?? entitySymbol.Name;
            AddEntityTableMapping(projectContext, graphBuilder, entitySymbol, inferredTable, Certainty.Inferred);
        }
    }

    private static void RegisterToTableMappings(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        TypeDeclarationSyntax typeSyntax,
        Dictionary<string, string> entityTableMappings)
    {
        foreach (var invocation in typeSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            if (!string.Equals(methodSymbol.Name, "Entity", StringComparison.Ordinal) || methodSymbol.TypeArguments.Length != 1)
            {
                continue;
            }

            var entitySymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;
            if (entitySymbol is null)
            {
                continue;
            }

            var parentInvocation = invocation.Parent?.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(candidate =>
                    semanticModel.GetSymbolInfo(candidate).Symbol is IMethodSymbol candidateSymbol
                    && string.Equals(candidateSymbol.Name, "ToTable", StringComparison.Ordinal));

            var tableName = parentInvocation is not null
                ? TryGetString(parentInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression, semanticModel)
                : entitySymbol.Name;

            if (tableName is null)
            {
                continue;
            }

            entityTableMappings[SymbolUtilities.CreateTypeId(entitySymbol)] = tableName;
            AddEntityTableMapping(projectContext, graphBuilder, entitySymbol, tableName, parentInvocation is null ? Certainty.Inferred : Certainty.Exact);
        }
    }

    private static void AddEntityTableMapping(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        INamedTypeSymbol entitySymbol,
        string tableName,
        Certainty certainty)
    {
        var entityId = SymbolUtilities.CreateTypeId(entitySymbol);
        var tableId = GraphIdFactory.Create("table", projectContext.RepositoryName ?? projectContext.ProjectName, tableName);

        graphBuilder.AddNode(
            entityId,
            GraphNodeType.Entity,
            entitySymbol.Name,
            SymbolUtilities.ToSourceLocation(entitySymbol),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            certainty,
            new Dictionary<string, string?> { ["fullName"] = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) });

        graphBuilder.AddNode(
            tableId,
            GraphNodeType.Table,
            tableName,
            repositoryName: projectContext.RepositoryName,
            projectName: projectContext.ProjectName,
            certainty: certainty,
            metadata: new Dictionary<string, string?> { ["table"] = tableName });

        graphBuilder.AddEdge(
            entityId,
            tableId,
            GraphEdgeType.MAPS_TO_TABLE,
            $"{entitySymbol.Name} maps to {tableName}",
            SymbolUtilities.ToSourceLocation(entitySymbol),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            certainty);
    }

    private static void AnalyzeMethod(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        BaseMethodDeclarationSyntax methodSyntax,
        Dictionary<string, string> entityTableMappings,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var methodId = SymbolUtilities.CreateMethodId(methodSymbol);

        foreach (var node in methodSyntax.DescendantNodes())
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    TrackMemberAccess(projectContext, graphBuilder, semanticModel, methodSymbol, methodId, memberAccess);
                    break;

                case IdentifierNameSyntax identifier:
                    TrackIdentifierUsage(projectContext, graphBuilder, semanticModel, methodSymbol, methodId, identifier, entityTableMappings);
                    break;

                case InvocationExpressionSyntax invocation:
                    TrackRawSql(projectContext, graphBuilder, semanticModel, methodSymbol, methodId, invocation);
                    break;
            }
        }
    }

    private static void TrackMemberAccess(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        string methodId,
        MemberAccessExpressionSyntax memberAccess)
    {
        var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type as INamedTypeSymbol;
        if (receiverType is null || !SymbolUtilities.InheritsFrom(receiverType, "Microsoft.EntityFrameworkCore.DbContext"))
        {
            return;
        }

        var dbContextId = SymbolUtilities.CreateTypeId(receiverType);
        graphBuilder.AddNode(
            dbContextId,
            GraphNodeType.DbContext,
            receiverType.Name,
            SymbolUtilities.ToSourceLocation(receiverType),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact);

        graphBuilder.AddEdge(
            methodId,
            dbContextId,
            GraphEdgeType.USES_DBCONTEXT,
            $"{methodSymbol.Name} uses {receiverType.Name}",
            SymbolUtilities.ToSourceLocation(memberAccess),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            Certainty.Exact);
    }

    private static void TrackIdentifierUsage(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        string methodId,
        IdentifierNameSyntax identifier,
        Dictionary<string, string> entityTableMappings)
    {
        var type = semanticModel.GetTypeInfo(identifier).Type as INamedTypeSymbol;
        if (type is null)
        {
            return;
        }

        if (SymbolUtilities.InheritsFrom(type, "Microsoft.EntityFrameworkCore.DbContext"))
        {
            var dbContextId = SymbolUtilities.CreateTypeId(type);
            graphBuilder.AddNode(
                dbContextId,
                GraphNodeType.DbContext,
                type.Name,
                SymbolUtilities.ToSourceLocation(type),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);

            graphBuilder.AddEdge(
                methodId,
                dbContextId,
                GraphEdgeType.USES_DBCONTEXT,
                $"{methodSymbol.Name} uses {type.Name}",
                SymbolUtilities.ToSourceLocation(identifier),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);
            return;
        }

        if (type.Name == "DbSet" && type.TypeArguments.Length == 1 && type.TypeArguments[0] is INamedTypeSymbol entitySymbol)
        {
            var entityId = SymbolUtilities.CreateTypeId(entitySymbol);
            graphBuilder.AddNode(
                entityId,
                GraphNodeType.Entity,
                entitySymbol.Name,
                SymbolUtilities.ToSourceLocation(entitySymbol),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Inferred);

            graphBuilder.AddEdge(
                methodId,
                entityId,
                GraphEdgeType.QUERIES_ENTITY,
                $"{methodSymbol.Name} queries {entitySymbol.Name}",
                SymbolUtilities.ToSourceLocation(identifier),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Inferred);

            if (entityTableMappings.TryGetValue(entityId, out var tableName))
            {
                var tableId = GraphIdFactory.Create("table", projectContext.RepositoryName ?? projectContext.ProjectName, tableName);
                graphBuilder.AddNode(tableId, GraphNodeType.Table, tableName, repositoryName: projectContext.RepositoryName, projectName: projectContext.ProjectName, certainty: Certainty.Inferred);
                graphBuilder.AddEdge(
                    methodId,
                    tableId,
                    GraphEdgeType.DEPENDS_ON,
                    $"{methodSymbol.Name} touches {tableName}",
                    SymbolUtilities.ToSourceLocation(identifier),
                    projectContext.RepositoryName,
                    projectContext.ProjectName,
                    Certainty.Inferred);
            }
        }
    }

    private static void TrackRawSql(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        string methodId,
        InvocationExpressionSyntax invocation)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invokedSymbol)
        {
            return;
        }

        if (!invokedSymbol.Name.Contains("Sql", StringComparison.Ordinal))
        {
            return;
        }

        var sqlText = invocation.ArgumentList.Arguments
            .Select(argument => TryGetString(argument.Expression, semanticModel))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (string.IsNullOrWhiteSpace(sqlText))
        {
            return;
        }

        foreach (Match match in SqlTableRegex().Matches(sqlText))
        {
            var tableName = match.Groups["table"].Value;
            var tableId = GraphIdFactory.Create("table", projectContext.RepositoryName ?? projectContext.ProjectName, tableName);
            graphBuilder.AddNode(
                tableId,
                GraphNodeType.Table,
                tableName,
                repositoryName: projectContext.RepositoryName,
                projectName: projectContext.ProjectName,
                certainty: Certainty.Ambiguous,
                metadata: new Dictionary<string, string?> { ["rawSql"] = sqlText });

            graphBuilder.AddEdge(
                methodId,
                tableId,
                GraphEdgeType.DEPENDS_ON,
                $"{methodSymbol.Name} references {tableName} in SQL",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Ambiguous,
                new Dictionary<string, string?> { ["sql"] = sqlText });
        }
    }

    private static string? TryGetTableFromAttributes(INamedTypeSymbol entitySymbol)
    {
        foreach (var attribute in entitySymbol.GetAttributes())
        {
            if (!string.Equals(attribute.AttributeClass?.Name, "TableAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            return attribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
        }

        foreach (var syntaxReference in entitySymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeSyntax)
            {
                continue;
            }

            foreach (var attributeList in typeSyntax.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!attribute.Name.ToString().Contains("Table", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal)
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
        }

        return null;
    }

    private static string? TryGetString(ExpressionSyntax? expression, SemanticModel semanticModel)
    {
        if (expression is null)
        {
            return null;
        }

        var constant = semanticModel.GetConstantValue(expression);
        return constant.HasValue && constant.Value is not null ? constant.Value.ToString() : null;
    }

    [GeneratedRegex(@"\b(?:from|join|update|into|table)\s+(?<table>[A-Za-z0-9_\.\[\]]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SqlTableRegex();
}
