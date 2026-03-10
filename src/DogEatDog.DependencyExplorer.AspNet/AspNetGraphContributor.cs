using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using DogEatDog.DependencyExplorer.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.AspNet;

public sealed class AspNetGraphContributor : IRoslynGraphContributor
{
    private static readonly Dictionary<string, string> VerbByAttributeName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HttpGetAttribute"] = "GET",
        ["HttpPostAttribute"] = "POST",
        ["HttpPutAttribute"] = "PUT",
        ["HttpDeleteAttribute"] = "DELETE",
        ["HttpPatchAttribute"] = "PATCH",
        ["HttpHeadAttribute"] = "HEAD",
        ["HttpOptionsAttribute"] = "OPTIONS"
    };

    private static readonly Dictionary<string, string> VerbByMapMethod = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MapGet"] = "GET",
        ["MapPost"] = "POST",
        ["MapPut"] = "PUT",
        ["MapDelete"] = "DELETE",
        ["MapPatch"] = "PATCH"
    };

    public string Name => nameof(AspNetGraphContributor);

    public Task ContributeAsync(RoslynProjectContext projectContext, GraphBuilder graphBuilder, CancellationToken cancellationToken)
    {
        foreach (var syntaxTree in projectContext.Compilation.SyntaxTrees)
        {
            if (!projectContext.Workspace.Options.IncludeGeneratedFiles && SymbolUtilities.IsGeneratedFile(syntaxTree.FilePath))
            {
                continue;
            }

            var semanticModel = projectContext.Compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot(cancellationToken);

            foreach (var controller in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(controller, cancellationToken) is not INamedTypeSymbol controllerSymbol)
                {
                    continue;
                }

                if (SymbolUtilities.ClassifyType(controllerSymbol) != GraphNodeType.Controller)
                {
                    continue;
                }

                AddControllerEndpoints(projectContext, graphBuilder, semanticModel, controller, controllerSymbol, cancellationToken);
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                AddMinimalApiEndpoint(projectContext, graphBuilder, semanticModel, invocation, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    private static void AddControllerEndpoints(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        ClassDeclarationSyntax controllerSyntax,
        INamedTypeSymbol controllerSymbol,
        CancellationToken cancellationToken)
    {
        var controllerId = SymbolUtilities.CreateTypeId(controllerSymbol);
        var classRoute = GetRouteTemplate(controllerSymbol, semanticModel, controllerSyntax);

        foreach (var methodSyntax in controllerSyntax.Members.OfType<MethodDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            var verb = GetHttpVerb(methodSymbol);
            if (verb is null)
            {
                continue;
            }

            var methodRoute = GetRouteTemplate(methodSymbol, semanticModel, methodSyntax);
            var route = NormalizeRoute(controllerSymbol.Name, methodSymbol.Name, classRoute, methodRoute);
            var endpointId = GraphIdFactory.Create("endpoint", projectContext.ProjectName, verb, route, SymbolUtilities.CreateMethodId(methodSymbol));
            var displayName = $"{verb} {route}";

            graphBuilder.AddNode(
                endpointId,
                GraphNodeType.Endpoint,
                displayName,
                SymbolUtilities.ToSourceLocation(methodSyntax),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact,
                new Dictionary<string, string?>
                {
                    ["verb"] = verb,
                    ["route"] = route,
                    ["handler"] = SymbolUtilities.CreateMethodId(methodSymbol),
                    ["controller"] = controllerSymbol.Name
                });

            graphBuilder.AddEdge(
                controllerId,
                endpointId,
                GraphEdgeType.EXPOSES,
                $"{controllerSymbol.Name} exposes {displayName}",
                SymbolUtilities.ToSourceLocation(methodSyntax),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);

            graphBuilder.AddEdge(
                endpointId,
                SymbolUtilities.CreateMethodId(methodSymbol),
                GraphEdgeType.CALLS,
                $"{displayName} invokes {controllerSymbol.Name}.{methodSymbol.Name}",
                SymbolUtilities.ToSourceLocation(methodSyntax),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Exact);
        }
    }

    private static void AddMinimalApiEndpoint(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!VerbByMapMethod.TryGetValue(methodSymbol.Name, out var verb))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count < 2)
        {
            return;
        }

        var route = TryGetStringValue(invocation.ArgumentList.Arguments[0].Expression, semanticModel) ?? "/unknown";
        var handlerExpression = invocation.ArgumentList.Arguments[1].Expression;
        string endpointId;
        string? handlerId = null;
        var certainty = Certainty.Exact;

        if (semanticModel.GetSymbolInfo(handlerExpression, cancellationToken).Symbol is IMethodSymbol handlerMethod)
        {
            handlerId = SymbolUtilities.CreateMethodId(handlerMethod);
        }
        else if (handlerExpression is LambdaExpressionSyntax lambda)
        {
            handlerId = SymbolUtilities.CreateSyntheticMethodId(projectContext.ProjectName, lambda, $"{verb} {route}");
            graphBuilder.AddNode(
                handlerId,
                GraphNodeType.Method,
                $"lambda {verb} {route}",
                SymbolUtilities.ToSourceLocation(lambda),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                Certainty.Inferred,
                new Dictionary<string, string?> { ["synthetic"] = "true" });

            foreach (var nestedInvocation in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(nestedInvocation, cancellationToken).Symbol is not IMethodSymbol targetMethod || !SymbolUtilities.IsSourceSymbol(targetMethod))
                {
                    continue;
                }

                var targetId = SymbolUtilities.CreateMethodId(targetMethod);
                graphBuilder.AddNode(
                    targetId,
                    GraphNodeType.Method,
                    SymbolUtilities.GetMethodDisplayName(targetMethod),
                    SymbolUtilities.ToSourceLocation(targetMethod),
                    projectContext.RepositoryName,
                    projectContext.ProjectName,
                    Certainty.Exact);

                graphBuilder.AddEdge(
                    handlerId,
                    targetId,
                    GraphEdgeType.CALLS,
                    $"lambda {verb} {route} calls {targetMethod.Name}",
                    SymbolUtilities.ToSourceLocation(nestedInvocation),
                    projectContext.RepositoryName,
                    projectContext.ProjectName,
                    Certainty.Inferred);
            }
        }
        else
        {
            endpointId = GraphIdFactory.Create("endpoint", projectContext.ProjectName, verb, route, invocation.SyntaxTree.FilePath, invocation.SpanStart.ToString());
            certainty = Certainty.Ambiguous;
            graphBuilder.AddNode(
                endpointId,
                GraphNodeType.Endpoint,
                $"{verb} {route}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                certainty);
            return;
        }

        endpointId = GraphIdFactory.Create("endpoint", projectContext.ProjectName, verb, route, handlerId);
        graphBuilder.AddNode(
            endpointId,
            GraphNodeType.Endpoint,
            $"{verb} {route}",
            SymbolUtilities.ToSourceLocation(invocation),
            projectContext.RepositoryName,
            projectContext.ProjectName,
            certainty,
            new Dictionary<string, string?>
            {
                ["verb"] = verb,
                ["route"] = route,
                ["minimalApi"] = "true",
                ["handler"] = handlerId
            });

        if (handlerId is not null)
        {
            graphBuilder.AddEdge(
                endpointId,
                handlerId,
                GraphEdgeType.CALLS,
                $"{verb} {route} invokes handler",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                certainty);
        }
    }

    private static string? GetHttpVerb(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name is not null && VerbByAttributeName.TryGetValue(name, out var verb))
            {
                return verb;
            }

            if (string.Equals(name, "AcceptVerbsAttribute", StringComparison.OrdinalIgnoreCase)
                && attribute.ConstructorArguments.FirstOrDefault() is { Kind: TypedConstantKind.Array } verbs
                && verbs.Values.FirstOrDefault().Value is string acceptVerb)
            {
                return acceptVerb.ToUpperInvariant();
            }
        }

        return null;
    }

    private static string? GetRouteTemplate(ISymbol symbol, SemanticModel semanticModel, SyntaxNode syntaxNode)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (string.Equals(name, "RouteAttribute", StringComparison.OrdinalIgnoreCase))
            {
                return attribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
            }

            if (name is not null && VerbByAttributeName.ContainsKey(name) && attribute.ConstructorArguments.Length == 1)
            {
                return attribute.ConstructorArguments[0].Value?.ToString();
            }
        }

        var routeAttribute = syntaxNode.DescendantNodes()
            .OfType<AttributeSyntax>()
            .FirstOrDefault(attribute => attribute.Name.ToString().Contains("Route", StringComparison.OrdinalIgnoreCase));

        return routeAttribute?.ArgumentList?.Arguments.Count > 0
            ? TryGetStringValue(routeAttribute.ArgumentList.Arguments[0].Expression, semanticModel)
            : null;
    }

    private static string NormalizeRoute(string controllerName, string actionName, string? classRoute, string? methodRoute)
    {
        var baseRoute = string.IsNullOrWhiteSpace(classRoute) ? string.Empty : classRoute.Trim('/');
        var actionRoute = string.IsNullOrWhiteSpace(methodRoute) ? string.Empty : methodRoute.Trim('/');
        var route = string.Join('/', new[] { baseRoute, actionRoute }.Where(value => !string.IsNullOrWhiteSpace(value)));

        route = route
            .Replace("[controller]", controllerName.Replace("Controller", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);

        return "/" + route.Trim('/');
    }

    private static string? TryGetStringValue(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var constant = semanticModel.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is not null)
        {
            return constant.Value.ToString();
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            var parts = new List<string>();
            foreach (var content in interpolated.Contents)
            {
                switch (content)
                {
                    case InterpolatedStringTextSyntax text:
                        parts.Add(text.TextToken.ValueText);
                        break;
                    case InterpolationSyntax interpolation:
                        var interpolationValue = semanticModel.GetConstantValue(interpolation.Expression);
                        if (!interpolationValue.HasValue || interpolationValue.Value is null)
                        {
                            return null;
                        }

                        parts.Add(interpolationValue.Value.ToString() ?? string.Empty);
                        break;
                }
            }

            return string.Concat(parts);
        }

        return null;
    }
}
