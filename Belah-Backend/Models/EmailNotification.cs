using System.ComponentModel.DataAnnotations;

namespace Belah_Backend.Models;

public sealed class EmailNotification
{
    public long Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, StringLength(160)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, StringLength(200), EmailAddress]
    public string RecipientEmail { get; set; } = string.Empty;

    [Required, StringLength(3)]
    public string Currency { get; set; } = "ZAR";

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    public int Attempts { get; set; }
    public int TotalAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    [StringLength(160)]
    public string? LastErrorCode { get; set; }
}
