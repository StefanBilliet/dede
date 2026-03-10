using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Orders.Core;
using Orders.Data;

namespace Orders.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController(IOrdersService ordersService) : ControllerBase
{
    [HttpGet("{id}")]
    public string Get(int id) => ordersService.GetOrder(id);
}

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOrdersService, OrdersService>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICatalogGateway, CatalogGateway>();
    }
}
