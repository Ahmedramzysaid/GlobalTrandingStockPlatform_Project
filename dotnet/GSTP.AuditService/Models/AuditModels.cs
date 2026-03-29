using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GSTP.AuditService.Models;

[Table("audit_logs")]
public class AuditLog
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("event_type")] [MaxLength(100)] public string EventType { get; set; } = string.Empty;
    [Column("entity_type")] [MaxLength(100)] public string? EntityType { get; set; }
    [Column("entity_id")] [MaxLength(100)] public string? EntityId { get; set; }
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("action")] [MaxLength(50)] public string Action { get; set; } = string.Empty;
    [Column("old_values")] public string? OldValues { get; set; }
    [Column("new_values")] public string? NewValues { get; set; }
    [Column("ip_address")] [MaxLength(45)] public string? IpAddress { get; set; }
    [Column("user_agent")] [MaxLength(500)] public string? UserAgent { get; set; }
    [Column("correlation_id")] [MaxLength(100)] public string? CorrelationId { get; set; }
    [Column("source_service")] [MaxLength(100)] public string? SourceService { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace GSTP.AuditService.Data;

public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.EventType);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.CreatedAt);
        });
    }
}
