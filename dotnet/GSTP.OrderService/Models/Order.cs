using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GSTP.OrderService.Models;

[Table("orders")]
public class Order
{
    [Key] [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")] public Guid UserId { get; set; }
    [Required] [Column("symbol")] [MaxLength(10)] public string Symbol { get; set; } = string.Empty;
    [Required] [Column("side")] [MaxLength(4)] public string Side { get; set; } = string.Empty;
    [Required] [Column("type")] [MaxLength(15)] public string Type { get; set; } = string.Empty;
    [Required] [Column("status")] [MaxLength(15)] public string Status { get; set; } = "PENDING";
    [Column("quantity")] public decimal Quantity { get; set; }
    [Column("filled_quantity")] public decimal FilledQuantity { get; set; } = 0;
    [Column("price")] public decimal? Price { get; set; }
    [Column("stop_price")] public decimal? StopPrice { get; set; }
    [Column("trail_percent")] public decimal? TrailPercent { get; set; }
    [Column("time_in_force")] [MaxLength(5)] public string TimeInForce { get; set; } = "DAY";
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }
    [Column("idempotency_key")] [MaxLength(64)] public string? IdempotencyKey { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderFill> Fills { get; set; } = new List<OrderFill>();
}

[Table("order_fills")]
public class OrderFill
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("order_id")] public Guid OrderId { get; set; }
    [Column("trade_id")] public Guid TradeId { get; set; }
    [Column("fill_price")] public decimal FillPrice { get; set; }
    [Column("fill_quantity")] public decimal FillQuantity { get; set; }
    [Column("commission")] public decimal Commission { get; set; } = 0;
    [Column("filled_at")] public DateTime FilledAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("OrderId")] public Order Order { get; set; } = null!;
}

// DTOs
public class CreateOrderDto
{
    [Required] public string Symbol { get; set; } = string.Empty;
    [Required] public string Side { get; set; } = string.Empty;
    [Required] public string Type { get; set; } = string.Empty;
    [Required] [Range(0.001, 1000000)] public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPrice { get; set; }
    public string TimeInForce { get; set; } = "DAY";
    public string? IdempotencyKey { get; set; }
}
