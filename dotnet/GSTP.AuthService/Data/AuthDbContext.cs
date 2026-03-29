using GSTP.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace GSTP.AuthService.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<UserAuth> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAuth>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasMany(e => e.RefreshTokens)
                  .WithOne(t => t.User)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Token);
        });
    }
}
