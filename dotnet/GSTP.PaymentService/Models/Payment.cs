using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GSTP.PaymentService.Models;

[Table("accounts")]
public class Account
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("balance")] public decimal Balance { get; set; } = 0;
    [Column("reserved")] public decimal Reserved { get; set; } = 0;
    [Column("currency")] [MaxLength(3)] public string Currency { get; set; } = "USD";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

[Table("transactions")]
public class Transaction
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("account_id")] public Guid AccountId { get; set; }
    [Required] [Column("type")] [MaxLength(15)] public string Type { get; set; } = string.Empty;
    [Required] [Column("status")] [MaxLength(15)] public string Status { get; set; } = "PENDING";
    [Column("amount")] public decimal Amount { get; set; }
    [Column("fee")] public decimal Fee { get; set; } = 0;
    [Column("reference_id")] [MaxLength(128)] public string? ReferenceId { get; set; }
    [Column("description")] public string? Description { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    [ForeignKey("AccountId")] public Account Account { get; set; } = null!;
}

[Table("ledger_entries")]
public class LedgerEntry
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("transaction_id")] public Guid TransactionId { get; set; }
    [Column("account_id")] public Guid AccountId { get; set; }
    [Column("debit")] public decimal Debit { get; set; } = 0;
    [Column("credit")] public decimal Credit { get; set; } = 0;
    [Column("balance_after")] public decimal BalanceAfter { get; set; }
    [Column("entry_date")] public DateTime EntryDate { get; set; } = DateTime.UtcNow;
}

// DTOs
public class DepositDto
{
    [Required] [Range(10, 1000000)] public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; } = "BANK_TRANSFER";
}

public class WithdrawDto
{
    [Required] [Range(10, 50000)] public decimal Amount { get; set; }
    public string? BankAccount { get; set; }
}
