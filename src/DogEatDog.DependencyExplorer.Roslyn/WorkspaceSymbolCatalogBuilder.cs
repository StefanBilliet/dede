using DogEatDog.DependencyExplorer.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal static class WorkspaceSymbolCatalogBuilder
{
    private static readonly HashSet<string> RegistrationMethodNames = new(StringComparer.Ordinal)
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient",
        "TryAddScoped",
        "TryAddSingleton",
        "TryAddTransient"
    };

    public static WorkspaceSymbolCatalog Build(IEnumerable<RoslynProjectContext> projectContexts, WorkspaceScanOptions options)
    {
        var catalog = new WorkspaceSymbolCatalog();

        foreach (var projectContext in projectContexts)
        {
            foreach (var syntaxTree in projectContext.Compilation.SyntaxTrees)
            {
                var filePath = syntaxTree.FilePath;
                if (!options.IncludeGeneratedFiles && SymbolUtilities.IsGeneratedFile(filePath))
                {
                    continue;
                }

                var semanticModel = projectContext.Compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                foreach (var typeSyntax in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (semanticModel.GetDeclaredSymbol(typeSyntax) is not INamedTypeSymbol typeSymbol)
                    {
                        continue;
                    }

                    var typeReference = new TypeReference(
                        SymbolUtilities.CreateTypeId(typeSymbol),
                        SymbolUtilities.GetTypeDisplayName(typeSymbol),
                        SymbolUtilities.ToSourceLocation(typeSymbol),
                        projectContext.RepositoryName,
                        projectContext.ProjectName);

                    foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
                    {
                        var interfaceId = SymbolUtilities.CreateTypeId(interfaceSymbol);
                        catalog.ImplementationsByInterfaceId.GetOrAdd(interfaceId).Add(typeReference);

                        foreach (var interfaceMember in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
                        {
                            var implementationMember = typeSymbol.FindImplementationForInterfaceMember(interfaceMember) as IMethodSymbol;
                            if (implementationMember is null)
                            {
                                continue;
                            }

                            var methodReference = new MethodReference(
                                SymbolUtilities.CreateMethodId(implementationMember),
                                SymbolUtilities.CreateTypeId(typeSymbol),
                                SymbolUtilities.GetMethodDisplayName(implementationMember),
                                SymbolUtilities.ToSourceLocation(implementationMember),
                                projectContext.RepositoryName,
                                projectContext.ProjectName);

                            catalog.ImplementationMethodsByInterfaceMethodId
                                .GetOrAdd(SymbolUtilities.CreateMethodId(interfaceMember))
                                .AddDistinctMethodReference(methodReference);

                            if (string.Equals(interfaceMember.Name, "Handle", StringComparison.Ordinal)
                                && MediatRSymbolMatcher.TryGetRequestHandlerRequestTypeId(interfaceSymbol) is { } requestTypeId)
                            {
                                catalog.MediatR.RequestHandlerMethodsByRequestTypeId
                                    .GetOrAdd(requestTypeId)
                                    .AddDistinctMethodReference(methodReference);
                            }

                            if (string.Equals(interfaceMember.Name, "Handle", StringComparison.Ordinal)
                                && MediatRSymbolMatcher.TryGetNotificationHandlerTypeId(interfaceSymbol) is { } notificationTypeId)
                            {
                                catalog.MediatR.NotificationHandlerMethodsByNotificationTypeId
                                    .GetOrAdd(notificationTypeId)
                                    .AddDistinctMethodReference(methodReference);
                            }
                        }
                    }
                }

                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invokedSymbol)
                    {
                        continue;
                    }

                    if (!RegistrationMethodNames.Contains(invokedSymbol.Name))
                    {
                        continue;
                    }

                    var registration = TryCreateRegistration(invocation, invokedSymbol, semanticModel, projectContext);
                    if (registration is null)
                    {
                        continue;
                    }

                    catalog.RegisteredImplementationsByInterfaceId.GetOrAdd(registration.InterfaceId).Add(registration);
                }
            }
        }

        return catalog;
    }

    private static ServiceRegistration? TryCreateRegistration(
        InvocationExpressionSyntax invocation,
        IMethodSymbol invokedSymbol,
        SemanticModel semanticModel,
        RoslynProjectContext projectContext)
    {
        INamedTypeSymbol? interfaceSymbol = null;
        INamedTypeSymbol? implementationSymbol = null;

        if (invokedSymbol.TypeArguments.Length == 2)
        {
            interfaceSymbol = invokedSymbol.TypeArguments[0] as INamedTypeSymbol;
            implementationSymbol = invokedSymbol.TypeArguments[1] as INamedTypeSymbol;
        }
        else if (invokedSymbol.TypeArguments.Length == 1)
        {
            interfaceSymbol = invokedSymbol.TypeArguments[0] as INamedTypeSymbol;
            implementationSymbol = interfaceSymbol;
        }
        else if (invocation.ArgumentList.Arguments.Count >= 2)
        {
            interfaceSymbol = ExtractTypeOf(invocation.ArgumentList.Arguments[0].Expression, semanticModel);
            implementationSymbol = ExtractTypeOf(invocation.ArgumentList.Arguments[1].Expression, semanticModel);
        }

        if ((interfaceSymbol is null || implementationSymbol is null)
            && invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName }
            && genericName.TypeArgumentList.Arguments.Count >= 2)
        {
            interfaceSymbol = semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
            implementationSymbol = semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[1]).Type as INamedTypeSymbol;
        }

        if (interfaceSymbol is null || implementationSymbol is null)
        {
            return null;
        }

        return new ServiceRegistration(
            SymbolUtilities.CreateTypeId(interfaceSymbol),
            SymbolUtilities.CreateTypeId(implementationSymbol),
            projectContext.RepositoryName ?? string.Empty,
            projectContext.ProjectName,
            SymbolUtilities.ToSourceLocation(invocation));
    }

    private static INamedTypeSymbol? ExtractTypeOf(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        if (expression is TypeOfExpressionSyntax typeOfExpression)
        {
            return semanticModel.GetTypeInfo(typeOfExpression.Type).Type as INamedTypeSymbol;
        }

        return semanticModel.GetTypeInfo(expression).Type as INamedTypeSymbol;
    }

    private static List<TValue> GetOrAdd<TValue>(this Dictionary<string, List<TValue>> source, string key)
    {
        if (!source.TryGetValue(key, out var list))
        {
            list = [];
            source[key] = list;
        }

        return list;
    }

    private static void AddDistinctMethodReference(this List<MethodReference> methods, MethodReference method)
    {
        if (methods.Any(existing => string.Equals(existing.Id, method.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        methods.Add(method);
    }
}
