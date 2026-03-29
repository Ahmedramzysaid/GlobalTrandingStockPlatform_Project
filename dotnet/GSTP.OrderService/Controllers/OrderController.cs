using GSTP.OrderService.Data;
using GSTP.OrderService.Kafka;
using GSTP.OrderService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSTP.OrderService.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly KafkaProducerService _kafka;
    private readonly ILogger<OrderController> _logger;

    public OrderController(OrderDbContext db, KafkaProducerService kafka, ILogger<OrderController> logger)
    { _db = db; _kafka = kafka; _logger = logger; }

    private Guid GetUserId() => Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, error = ModelState });

        // Idempotency check
        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            var existing = await _db.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == dto.IdempotencyKey);
            if (existing != null)
                return Ok(new { success = true, data = existing, message = "Duplicate order (idempotent)" });
        }

        var order = new Order
        {
            UserId = GetUserId(),
            Symbol = dto.Symbol.ToUpper(),
            Side = dto.Side.ToUpper(),
            Type = dto.Type.ToUpper(),
            Quantity = dto.Quantity,
            Price = dto.Price,
            StopPrice = dto.StopPrice,
            TimeInForce = dto.TimeInForce,
            IdempotencyKey = dto.IdempotencyKey,
            Status = "PENDING"
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Publish to Kafka
        await _kafka.ProduceAsync("orders.created", order.Symbol, new
        {
            EventId = Guid.NewGuid(),
            EventType = "OrderCreated",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                OrderId = order.Id, order.UserId, order.Symbol, order.Side, order.Type,
                order.Quantity, order.Price, order.StopPrice, order.TimeInForce
            }
        });

        _logger.LogInformation("Order created: {OrderId} {Side} {Quantity} {Symbol}", order.Id, order.Side, order.Quantity, order.Symbol);
        return Created($"/api/v1/orders/{order.Id}", new { success = true, data = order });
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] string? status, [FromQuery] string? symbol,
        [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var query = _db.Orders.Where(o => o.UserId == userId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status.ToUpper());
        if (!string.IsNullOrEmpty(symbol)) query = query.Where(o => o.Symbol == symbol.ToUpper());

        var total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * limit).Take(limit).ToListAsync();

        return Ok(new { success = true, data = orders, pagination = new { page, limit, total, totalPages = (int)Math.Ceiling((double)total / limit) } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Fills).FirstOrDefaultAsync(o => o.Id == id && o.UserId == GetUserId());
        if (order == null) return NotFound(new { success = false, error = "Order not found" });
        return Ok(new { success = true, data = order });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == GetUserId());
        if (order == null) return NotFound();
        if (order.Status is "FILLED" or "CANCELLED") return BadRequest(new { error = "Cannot cancel this order" });

        order.Status = "CANCELLED";
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _kafka.ProduceAsync("orders.cancelled", order.Symbol, new { OrderId = order.Id, order.Symbol, Timestamp = DateTime.UtcNow });
        return Ok(new { success = true, message = "Order cancelled" });
    }
}
