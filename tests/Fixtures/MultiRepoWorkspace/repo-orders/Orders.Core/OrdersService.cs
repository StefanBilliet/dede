using System.Net.Http;

namespace Orders.Core;

public interface IOrdersService
{
    string GetOrder(int id);
}

public interface ICatalogGateway
{
    Task<string> GetProductSummaryAsync(int id);
}

public sealed class OrdersService(IOrderRepository repository, ICatalogGateway catalogGateway) : IOrdersService
{
    public string GetOrder(int id)
    {
        repository.Load(id);
        return catalogGateway.GetProductSummaryAsync(id).GetAwaiter().GetResult();
    }
}

public interface IOrderRepository
{
    Order? Load(int id);
}

public sealed class CatalogGateway(IHttpClientFactory httpClientFactory) : ICatalogGateway
{
    public async Task<string> GetProductSummaryAsync(int id)
    {
        var client = httpClientFactory.CreateClient("CatalogClient");
        return await client.GetAsync($"/products/{id}").ContinueWith(_ => "ok");
    }
}

public interface IHttpClientFactory
{
    HttpClient CreateClient(string name);
}
