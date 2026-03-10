namespace Microsoft.AspNetCore.Builder
{
    public interface IEndpointRouteBuilder;

    public static class EndpointRouteBuilderExtensions
    {
        public static void MapGet(this IEndpointRouteBuilder builder, string pattern, Func<int, string> handler)
        {
        }
    }
}

namespace Catalog.Api
{
    using Microsoft.AspNetCore.Builder;

    public static class ProgramFile
    {
        public static void Configure(IEndpointRouteBuilder app)
        {
            app.MapGet("/products/{id}", GetProduct);
        }

        public static string GetProduct(int id) => id.ToString();
    }
}
