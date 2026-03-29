using GSTP.AuditService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSTP.AuditService.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AuditController : ControllerBase
{
    private readonly AuditDbContext _db;

    public AuditController(AuditDbContext db) => _db = db;

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] string? eventType, [FromQuery] string? userId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(eventType)) query = query.Where(a => a.EventType == eventType);
        if (!string.IsNullOrEmpty(userId)) query = query.Where(a => a.UserId == Guid.Parse(userId));
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var logs = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync();

        return Ok(new { success = true, data = logs, pagination = new { page, limit, total } });
    }
}
