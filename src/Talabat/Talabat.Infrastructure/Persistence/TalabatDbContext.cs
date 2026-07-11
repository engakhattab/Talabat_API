using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;

namespace Talabat.Infrastructure.Persistence;

public sealed class TalabatDbContext : DbContext
{
    public TalabatDbContext(DbContextOptions<TalabatDbContext> options)
        : base(options)
    {
    }

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    public DbSet<DeliveryAgent> DeliveryAgents => Set<DeliveryAgent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TalabatDbContext).Assembly);
    }
}
