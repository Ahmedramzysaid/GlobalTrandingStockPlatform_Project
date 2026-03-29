using GSTP.OrderService.Models;
using Microsoft.EntityFrameworkCore;

namespace GSTP.OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderFill> OrderFills { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.UserId);
            e.HasIndex(o => new { o.Symbol, o.Status });
            e.HasIndex(o => o.IdempotencyKey).IsUnique();
            e.HasMany(o => o.Fills).WithOne(f => f.Order).HasForeignKey(f => f.OrderId);
        });
        modelBuilder.Entity<OrderFill>(e => e.HasIndex(f => f.OrderId));
    }
}
