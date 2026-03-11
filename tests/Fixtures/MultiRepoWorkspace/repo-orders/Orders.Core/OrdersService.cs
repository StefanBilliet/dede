using System.Net.Http;
using MediatR;

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

public sealed record GetOrderQuery(int Id) : IRequest<string>;

public sealed class GetOrderQueryHandler(IOrdersService ordersService) : IRequestHandler<GetOrderQuery, string>
{
    public Task<string> Handle(GetOrderQuery request, CancellationToken cancellationToken)
        => Task.FromResult(ordersService.GetOrder(request.Id));
}

public sealed record GetOrderProjectionQuery(int Id) : IRequest<string>;

public sealed class GetOrderProjectionPrimaryHandler(IOrdersService ordersService) : IRequestHandler<GetOrderProjectionQuery, string>
{
    public Task<string> Handle(GetOrderProjectionQuery request, CancellationToken cancellationToken)
        => Task.FromResult(ordersService.GetOrder(request.Id));
}

public sealed class GetOrderProjectionSecondaryHandler(IOrdersService ordersService) : IRequestHandler<GetOrderProjectionQuery, string>
{
    public Task<string> Handle(GetOrderProjectionQuery request, CancellationToken cancellationToken)
        => Task.FromResult(ordersService.GetOrder(request.Id));
}

public sealed record MissingHandlerQuery(int Id) : IRequest<string>;

public sealed record OrderViewedNotification(int Id) : INotification;

public sealed class OrderViewedNotificationHandler(IOrdersService ordersService) : INotificationHandler<OrderViewedNotification>
{
    public Task Handle(OrderViewedNotification notification, CancellationToken cancellationToken)
    {
        ordersService.GetOrder(notification.Id);
        return Task.CompletedTask;
    }
}

public sealed class OrderViewedAuditNotificationHandler : INotificationHandler<OrderViewedNotification>
{
    public Task Handle(OrderViewedNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
