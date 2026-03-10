namespace Microsoft.AspNetCore.Mvc
{
    public abstract class ControllerBase;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ApiControllerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RouteAttribute(string template) : Attribute
    {
        public string Template { get; } = template;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HttpGetAttribute(string? template = null) : Attribute
    {
        public string? Template { get; } = template;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HttpPostAttribute(string? template = null) : Attribute
    {
        public string? Template { get; } = template;
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection;

    public static class ServiceCollectionServiceExtensions
    {
        public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services)
            where TImplementation : TService => services;
    }
}

namespace Microsoft.Extensions.Configuration
{
    public interface IConfiguration
    {
        string this[string key] { get; }
    }
}

namespace Microsoft.EntityFrameworkCore
{
    public abstract class DbContext
    {
        protected virtual void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }

    public class DbSet<TEntity>;

    public sealed class ModelBuilder
    {
        public EntityTypeBuilder<TEntity> Entity<TEntity>() => new();
    }

    public sealed class EntityTypeBuilder<TEntity>
    {
        public void ToTable(string tableName)
        {
        }
    }
}

namespace System.ComponentModel.DataAnnotations.Schema
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TableAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public interface IEndpointRouteBuilder;

    public static class EndpointRouteBuilderExtensions
    {
        public static void MapGet(this IEndpointRouteBuilder builder, string pattern, Delegate handler)
        {
        }
    }
}
