using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using FakeMessaging;
using Orders.Core;
using Orders.Data;

namespace Orders.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController(IOrdersService ordersService, IMediator mediator, IDispatcher fakeDispatcher) : ControllerBase
{
    [HttpGet("{id}")]
    public string Get(int id) => ordersService.GetOrder(id);

    [HttpGet("mediated/{id}")]
    public string GetViaMediator(int id) => mediator.Send(new GetOrderQuery(id)).GetAwaiter().GetResult();

    [HttpGet("mediated-var/{id}")]
    public string GetViaMediatorVariable(int id)
    {
        var request = new GetOrderQuery(id);
        return mediator.Send(request).GetAwaiter().GetResult();
    }

    [HttpGet("mediated-generic/{id}")]
    public async Task<string> GetViaMediatorGenericAsync(int id, CancellationToken cancellationToken)
        => await mediator.Send<string>(new GetOrderQuery(id), cancellationToken);

    [HttpGet("projection/{id}")]
    public string GetProjectionViaMediator(int id) => mediator.Send(new GetOrderProjectionQuery(id)).GetAwaiter().GetResult();

    [HttpGet("missing-handler/{id}")]
    public string GetViaMediatorMissingHandler(int id) => mediator.Send(new MissingHandlerQuery(id)).GetAwaiter().GetResult();

    [HttpPost("notify/{id}")]
    public Task PublishOrderViewed(int id, CancellationToken cancellationToken)
        => mediator.Publish(new OrderViewedNotification(id), cancellationToken);

    [HttpGet("fake-dispatch/{id}")]
    public string GetViaFakeDispatcher(int id) => fakeDispatcher.Send(new GetOrderQuery(id));
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
