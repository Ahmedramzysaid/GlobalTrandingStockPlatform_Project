using GSTP.PaymentService.Models;
using Microsoft.EntityFrameworkCore;

namespace GSTP.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<LedgerEntry> LedgerEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e => { e.HasIndex(a => a.UserId).IsUnique(); });
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasIndex(t => t.AccountId);
            e.HasIndex(t => t.Status);
        });
    }
}
