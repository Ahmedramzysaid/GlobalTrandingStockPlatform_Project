using GSTP.PaymentService.Data;
using GSTP.PaymentService.Kafka;
using GSTP.PaymentService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSTP.PaymentService.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly PaymentDbContext _db;
    private readonly PaymentKafkaProducer _kafka;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(PaymentDbContext db, PaymentKafkaProducer kafka, ILogger<PaymentController> logger)
    { _db = db; _kafka = kafka; _logger = logger; }

    private Guid GetUserId() => Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var userId = GetUserId();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null)
        {
            account = new Account { UserId = userId };
            _db.Accounts.Add(account);
            await _db.SaveChangesAsync();
        }
        return Ok(new { success = true, data = new { account.Balance, account.Reserved, Available = account.Balance - account.Reserved, account.Currency } });
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, error = ModelState });
        var userId = GetUserId();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null)
        {
            account = new Account { UserId = userId };
            _db.Accounts.Add(account);
        }

        var txn = new Transaction
        {
            AccountId = account.Id, Type = "DEPOSIT", Status = "COMPLETED",
            Amount = dto.Amount, Description = $"Deposit via {dto.PaymentMethod}",
            CompletedAt = DateTime.UtcNow
        };
        _db.Transactions.Add(txn);

        account.Balance += dto.Amount;
        account.UpdatedAt = DateTime.UtcNow;

        _db.LedgerEntries.Add(new LedgerEntry
        {
            TransactionId = txn.Id, AccountId = account.Id,
            Debit = 0, Credit = dto.Amount, BalanceAfter = account.Balance
        });

        await _db.SaveChangesAsync();
        await _kafka.ProduceAsync("payments.completed", userId.ToString(), new
        {
            EventType = "DepositCompleted", UserId = userId, txn.Amount, txn.Id, Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("Deposit completed: {Amount} for user {UserId}", dto.Amount, userId);
        return Ok(new { success = true, data = new { transactionId = txn.Id, txn.Amount, txn.Status, newBalance = account.Balance } });
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, error = ModelState });
        var userId = GetUserId();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null) return NotFound(new { error = "Account not found" });

        var available = account.Balance - account.Reserved;
        if (available < dto.Amount)
            return BadRequest(new { success = false, error = new { code = "INSUFFICIENT_FUNDS", message = $"Available: {available:C}, Requested: {dto.Amount:C}" } });

        var txn = new Transaction
        {
            AccountId = account.Id, Type = "WITHDRAWAL", Status = "PROCESSING",
            Amount = dto.Amount, Description = "Withdrawal to bank account"
        };
        _db.Transactions.Add(txn);

        account.Balance -= dto.Amount;
        account.UpdatedAt = DateTime.UtcNow;

        _db.LedgerEntries.Add(new LedgerEntry
        {
            TransactionId = txn.Id, AccountId = account.Id,
            Debit = dto.Amount, Credit = 0, BalanceAfter = account.Balance
        });

        await _db.SaveChangesAsync();
        await _kafka.ProduceAsync("payments.completed", userId.ToString(), new
        {
            EventType = "WithdrawalProcessing", UserId = userId, txn.Amount, txn.Id, Timestamp = DateTime.UtcNow
        });

        return Ok(new { success = true, data = new { transactionId = txn.Id, txn.Amount, txn.Status, newBalance = account.Balance } });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null) return Ok(new { success = true, data = Array.Empty<object>() });

        var total = await _db.Transactions.CountAsync(t => t.AccountId == account.Id);
        var txns = await _db.Transactions.Where(t => t.AccountId == account.Id)
            .OrderByDescending(t => t.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync();

        return Ok(new { success = true, data = txns, pagination = new { page, limit, total } });
    }
}
