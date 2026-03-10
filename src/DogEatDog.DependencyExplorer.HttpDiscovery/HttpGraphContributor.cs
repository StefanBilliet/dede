using DogEatDog.DependencyExplorer.Core.Model;
using DogEatDog.DependencyExplorer.Graph;
using DogEatDog.DependencyExplorer.Graph.Model;
using DogEatDog.DependencyExplorer.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.HttpDiscovery;

public sealed class HttpGraphContributor : IRoslynGraphContributor
{
    private static readonly Dictionary<string, string> VerbByMethodName = new(StringComparer.Ordinal)
    {
        ["GetAsync"] = "GET",
        ["GetFromJsonAsync"] = "GET",
        ["PostAsync"] = "POST",
        ["PostAsJsonAsync"] = "POST",
        ["PutAsync"] = "PUT",
        ["PutAsJsonAsync"] = "PUT",
        ["DeleteAsync"] = "DELETE",
        ["PatchAsync"] = "PATCH",
        ["SendAsync"] = "SEND"
    };

    public string Name => nameof(HttpGraphContributor);

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

            foreach (var methodSyntax in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                AnalyzeMethod(projectContext, graphBuilder, semanticModel, methodSyntax, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    private static void AnalyzeMethod(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        SemanticModel semanticModel,
        BaseMethodDeclarationSyntax methodSyntax,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var methodId = SymbolUtilities.CreateMethodId(methodSymbol);
        var localClients = BuildLocalClientMap(projectContext, semanticModel, methodSyntax, cancellationToken);

        foreach (var invocation in methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol invokedSymbol)
            {
                continue;
            }

            if (!IsHttpCall(invokedSymbol))
            {
                continue;
            }

            var descriptor = ResolveClientDescriptor(projectContext, semanticModel, invocation, invokedSymbol, localClients);
            graphBuilder.AddNode(
                descriptor.Id,
                GraphNodeType.HttpClient,
                descriptor.DisplayName,
                descriptor.SourceLocation,
                projectContext.RepositoryName,
                projectContext.ProjectName,
                descriptor.Certainty,
                new Dictionary<string, string?>
                {
                    ["name"] = descriptor.Name,
                    ["baseUrl"] = descriptor.BaseUrl
                });

            graphBuilder.AddEdge(
                methodId,
                descriptor.Id,
                GraphEdgeType.USES_HTTP_CLIENT,
                $"{methodSymbol.ContainingType.Name}.{methodSymbol.Name} uses {descriptor.DisplayName}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                descriptor.Certainty);

            var requestPath = ExtractRequestPath(invocation, semanticModel, invokedSymbol);
            var verb = VerbByMethodName.GetValueOrDefault(invokedSymbol.Name, invokedSymbol.Name.Replace("Async", string.Empty, StringComparison.Ordinal));
            var requestUri = CombineUri(descriptor.BaseUrl, requestPath);
            var externalServiceName = requestUri?.Host ?? descriptor.Name ?? descriptor.DisplayName;
            var externalServiceId = GraphIdFactory.Create("external-service", externalServiceName ?? projectContext.ProjectName);
            var externalEndpointId = GraphIdFactory.Create("external-endpoint", verb, requestUri?.ToString() ?? requestPath ?? descriptor.DisplayName);

            graphBuilder.AddNode(
                externalServiceId,
                GraphNodeType.ExternalService,
                externalServiceName ?? "external-service",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                requestUri is null ? Certainty.Ambiguous : Certainty.Inferred,
                new Dictionary<string, string?> { ["host"] = requestUri?.Host });

            graphBuilder.AddNode(
                externalEndpointId,
                GraphNodeType.ExternalEndpoint,
                $"{verb} {(requestUri?.PathAndQuery ?? requestPath ?? "/unknown")}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                requestPath is null ? Certainty.Unresolved : Certainty.Inferred,
                new Dictionary<string, string?>
                {
                    ["verb"] = verb,
                    ["path"] = requestPath,
                    ["absoluteUri"] = requestUri?.ToString()
                });

            graphBuilder.AddEdge(
                descriptor.Id,
                externalServiceId,
                GraphEdgeType.CALLS_HTTP,
                $"{descriptor.DisplayName} targets {externalServiceName}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                requestUri is null ? Certainty.Ambiguous : Certainty.Inferred);

            graphBuilder.AddEdge(
                externalServiceId,
                externalEndpointId,
                GraphEdgeType.CALLS_HTTP,
                $"{externalServiceName} serves {verb} {(requestUri?.PathAndQuery ?? requestPath ?? "/unknown")}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                requestPath is null ? Certainty.Unresolved : Certainty.Inferred);

            ResolveInternalTarget(projectContext, graphBuilder, invocation, descriptor, requestUri, requestPath, externalEndpointId);
        }
    }

    private static Dictionary<ISymbol, HttpClientDescriptor> BuildLocalClientMap(
        RoslynProjectContext projectContext,
        SemanticModel semanticModel,
        BaseMethodDeclarationSyntax methodSyntax,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<ISymbol, HttpClientDescriptor>(SymbolEqualityComparer.Default);

        foreach (var variable in methodSyntax.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Initializer?.Value is null)
            {
                continue;
            }

            if (semanticModel.GetDeclaredSymbol(variable, cancellationToken) is not ISymbol localSymbol)
            {
                continue;
            }

            var descriptor = TryCreateDescriptorFromExpression(projectContext, semanticModel, variable.Initializer.Value, variable.Identifier.ValueText, cancellationToken);
            if (descriptor is not null)
            {
                map[localSymbol] = descriptor;
            }
        }

        return map;
    }

    private static HttpClientDescriptor ResolveClientDescriptor(
        RoslynProjectContext projectContext,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol invokedSymbol,
        Dictionary<ISymbol, HttpClientDescriptor> localClients)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var receiverSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
            if (receiverSymbol is not null && localClients.TryGetValue(receiverSymbol, out var localDescriptor))
            {
                return localDescriptor;
            }

            var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
            if (receiverType?.Name == "HttpClient")
            {
                return CreateDescriptor(
                    projectContext,
                    memberAccess.Expression.ToString(),
                    memberAccess.Expression.ToString(),
                    baseUrl: FindBaseUrlFromConfiguration(projectContext, memberAccess.Expression.ToString()),
                    SymbolUtilities.ToSourceLocation(invocation),
                    certainty: Certainty.Inferred);
            }
        }

        return CreateDescriptor(
            projectContext,
            invokedSymbol.ContainingType.Name,
            invokedSymbol.ContainingType.Name,
            FindBaseUrlFromConfiguration(projectContext, invokedSymbol.ContainingType.Name),
            SymbolUtilities.ToSourceLocation(invocation),
            Certainty.Ambiguous);
    }

    private static HttpClientDescriptor? TryCreateDescriptorFromExpression(
        RoslynProjectContext projectContext,
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        string fallbackName,
        CancellationToken cancellationToken)
    {
        if (expression is InvocationExpressionSyntax invocation
            && semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol symbol
            && string.Equals(symbol.Name, "CreateClient", StringComparison.Ordinal)
            && symbol.ContainingType.Name == "IHttpClientFactory")
        {
            var clientName = invocation.ArgumentList.Arguments.Count > 0
                ? TryGetStringValue(invocation.ArgumentList.Arguments[0].Expression, semanticModel)
                : fallbackName;

            return CreateDescriptor(
                projectContext,
                clientName ?? fallbackName,
                clientName ?? fallbackName,
                FindBaseUrlFromConfiguration(projectContext, clientName ?? fallbackName),
                SymbolUtilities.ToSourceLocation(invocation),
                Certainty.Inferred);
        }

        if (expression is ObjectCreationExpressionSyntax objectCreation
            && semanticModel.GetSymbolInfo(objectCreation, cancellationToken).Symbol is IMethodSymbol ctor
            && ctor.ContainingType.Name == "HttpClient")
        {
            return CreateDescriptor(
                projectContext,
                fallbackName,
                fallbackName,
                FindBaseUrlFromConfiguration(projectContext, fallbackName),
                SymbolUtilities.ToSourceLocation(objectCreation),
                Certainty.Ambiguous);
        }

        return null;
    }

    private static bool IsHttpCall(IMethodSymbol methodSymbol)
    {
        if (!VerbByMethodName.ContainsKey(methodSymbol.Name))
        {
            return false;
        }

        var containingType = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingType.Contains("HttpClient", StringComparison.Ordinal)
            || containingType.Contains("HttpClientJsonExtensions", StringComparison.Ordinal);
    }

    private static string? ExtractRequestPath(InvocationExpressionSyntax invocation, SemanticModel semanticModel, IMethodSymbol invokedSymbol)
    {
        if (invokedSymbol.Name == "SendAsync")
        {
            return null;
        }

        var argumentIndex = invokedSymbol.Name.StartsWith("Post", StringComparison.Ordinal)
            || invokedSymbol.Name.StartsWith("Put", StringComparison.Ordinal)
            || invokedSymbol.Name.StartsWith("Patch", StringComparison.Ordinal)
            ? 0
            : 0;

        return invocation.ArgumentList.Arguments.Count > argumentIndex
            ? TryGetStringValue(invocation.ArgumentList.Arguments[argumentIndex].Expression, semanticModel)
            : null;
    }

    private static void ResolveInternalTarget(
        RoslynProjectContext projectContext,
        GraphBuilder graphBuilder,
        InvocationExpressionSyntax invocation,
        HttpClientDescriptor descriptor,
        Uri? requestUri,
        string? requestPath,
        string externalEndpointId)
    {
        var path = requestUri?.AbsolutePath ?? requestPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var endpointCandidates = graphBuilder.Nodes
            .Where(node => node.Type == GraphNodeType.Endpoint)
            .Where(node =>
            {
                var route = node.Metadata.GetValueOrDefault("route");
                if (string.IsNullOrWhiteSpace(route))
                {
                    return false;
                }

                var routeMatches = RouteMatches(path, route)
                    || path.StartsWith(route, StringComparison.OrdinalIgnoreCase)
                    || route.StartsWith(path, StringComparison.OrdinalIgnoreCase);

                if (!routeMatches)
                {
                    return false;
                }

                if (requestUri is null)
                {
                    return node.ProjectName?.Contains(descriptor.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true
                        || node.RepositoryName?.Contains(descriptor.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true
                        || descriptor.BaseUrl?.Contains(node.ProjectName ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true
                        || descriptor.BaseUrl?.Contains(node.RepositoryName ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true;
                }

                return requestUri.Host.Contains(node.ProjectName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    || requestUri.Host.Contains(node.RepositoryName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();

        if (endpointCandidates.Length == 0)
        {
            endpointCandidates = graphBuilder.Nodes
                .Where(node => node.Type == GraphNodeType.Endpoint)
                .Where(node =>
                {
                    var route = node.Metadata.GetValueOrDefault("route");
                    if (string.IsNullOrWhiteSpace(route))
                    {
                        return false;
                    }

                    var routeHead = route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    var pathHead = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.Equals(routeHead, pathHead, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return string.Equals(node.RepositoryName, requestUri?.Host, StringComparison.OrdinalIgnoreCase)
                        || requestUri?.Host?.Contains(node.RepositoryName ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true
                        || descriptor.BaseUrl?.Contains(node.RepositoryName ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true;
                })
                .ToArray();
        }

        if (endpointCandidates.Length == 0 && requestUri is not null)
        {
            endpointCandidates = graphBuilder.Nodes
                .Where(node => node.Type == GraphNodeType.Endpoint)
                .Where(node => string.Equals(node.RepositoryName, requestUri.Host, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (endpointCandidates.Length == 0)
        {
            return;
        }

        var certainty = endpointCandidates.Length == 1 ? Certainty.Inferred : Certainty.Ambiguous;
        foreach (var candidate in endpointCandidates)
        {
            graphBuilder.AddEdge(
                externalEndpointId,
                candidate.Id,
                certainty == Certainty.Ambiguous ? GraphEdgeType.AMBIGUOUS : GraphEdgeType.RESOLVES_TO_SERVICE,
                $"{descriptor.DisplayName} resolves to {candidate.DisplayName}",
                SymbolUtilities.ToSourceLocation(invocation),
                projectContext.RepositoryName,
                projectContext.ProjectName,
                certainty);

            if (!string.Equals(projectContext.RepositoryName, candidate.RepositoryName, StringComparison.OrdinalIgnoreCase))
            {
                graphBuilder.AddEdge(
                    externalEndpointId,
                    candidate.Id,
                    GraphEdgeType.CROSSES_REPO_BOUNDARY,
                    $"{projectContext.ProjectName} calls across repo into {candidate.RepositoryName}",
                    SymbolUtilities.ToSourceLocation(invocation),
                    projectContext.RepositoryName,
                    projectContext.ProjectName,
                    certainty);
            }
        }
    }

    private static HttpClientDescriptor CreateDescriptor(
        RoslynProjectContext projectContext,
        string displayName,
        string name,
        string? baseUrl,
        SourceLocation? sourceLocation,
        Certainty certainty) =>
        new(
            GraphIdFactory.Create("http-client", projectContext.ProjectName, name),
            displayName,
            name,
            baseUrl,
            sourceLocation,
            certainty);

    private static string? FindBaseUrlFromConfiguration(RoslynProjectContext projectContext, string hint)
    {
        static bool LooksLikeUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out _);

        var repositoryScoped = projectContext.ConfigurationValues.ToArray();
        var workspaceScoped = projectContext.Workspace.Discovery.ConfigurationValues.ToArray();

        return repositoryScoped
                .Where(value => value.Key.Contains("url", StringComparison.OrdinalIgnoreCase)
                    || value.Key.Contains("baseurl", StringComparison.OrdinalIgnoreCase))
                .Where(value => value.Key.Contains(hint, StringComparison.OrdinalIgnoreCase)
                    || (value.Value?.Contains(hint, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(value => value.Value)
                .FirstOrDefault(LooksLikeUrl)
            ?? repositoryScoped
                .Where(value => value.Key.Contains("url", StringComparison.OrdinalIgnoreCase)
                    || value.Key.Contains("baseurl", StringComparison.OrdinalIgnoreCase))
                .Select(value => value.Value)
                .FirstOrDefault(LooksLikeUrl)
            ?? workspaceScoped
                .Where(value => value.Key.Contains(hint, StringComparison.OrdinalIgnoreCase))
                .Select(value => value.Value)
                .FirstOrDefault(LooksLikeUrl)
            ?? workspaceScoped
                .Select(value => value.Value)
                .FirstOrDefault(LooksLikeUrl);
    }

    private static Uri? CombineUri(string? baseUrl, string? requestPath)
    {
        if (!string.IsNullOrWhiteSpace(requestPath)
            && requestPath.Contains("://", StringComparison.Ordinal)
            && Uri.TryCreate(requestPath, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            && !string.IsNullOrWhiteSpace(requestPath)
            && Uri.TryCreate(baseUri, requestPath, out var combined))
        {
            return combined;
        }

        return null;
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
                        parts.Add(interpolationValue.HasValue && interpolationValue.Value is not null
                            ? interpolationValue.Value.ToString() ?? string.Empty
                            : "{value}");
                        break;
                }
            }

            return string.Concat(parts);
        }

        return null;
    }

    private sealed record HttpClientDescriptor(
        string Id,
        string DisplayName,
        string? Name,
        string? BaseUrl,
        SourceLocation? SourceLocation,
        Certainty Certainty);

    private static bool RouteMatches(string requestPath, string routeTemplate)
    {
        var requestSegments = requestPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routeSegments = routeTemplate.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (requestSegments.Length != routeSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < requestSegments.Length; index++)
        {
            var routeSegment = routeSegments[index];
            if (routeSegment.StartsWith("{", StringComparison.Ordinal) && routeSegment.EndsWith("}", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(requestSegments[index], routeSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
