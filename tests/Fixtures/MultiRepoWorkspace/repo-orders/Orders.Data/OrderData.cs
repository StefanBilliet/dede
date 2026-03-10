using Microsoft.EntityFrameworkCore;
using Orders.Core;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orders.Data;

[Table("orders")]
public sealed class Order
{
    public int Id { get; init; }
}

public sealed class OrdersDbContext : DbContext
{
    public DbSet<Order> Orders { get; } = new();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().ToTable("orders");
    }
}

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    public Order? Load(int id)
    {
        _ = dbContext.Orders;
        return null;
    }
}
