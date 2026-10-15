using Belah_Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/email-notifications")]
public sealed class AdminEmailNotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminEmailNotificationsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailNotificationStatusDto>>> List(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var query = _context.EmailNotifications.AsNoTracking().Include(n => n.Order).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (normalized is not ("Pending" or "Processing" or "Sent" or "Failed"))
                return BadRequest(new { message = "Status must be Pending, Processing, Sent, or Failed." });
            query = query.Where(n => n.Status == normalized);
        }

        var records = await query.OrderByDescending(n => n.CreatedAtUtc).Take(100)
            .Select(n => new EmailNotificationStatusDto
            {
                Id = n.Id,
                OrderNumber = n.Order!.OrderNumber,
                Status = n.Status,
                Attempts = n.Attempts,
                TotalAttempts = n.TotalAttempts,
                CreatedAtUtc = n.CreatedAtUtc,
                LastAttemptAtUtc = n.LastAttemptAtUtc,
                NextAttemptAtUtc = n.NextAttemptAtUtc,
                SentAtUtc = n.SentAtUtc,
                LastErrorCode = n.LastErrorCode
            })
            .ToListAsync(cancellationToken);
        return Ok(records);
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(long id, CancellationToken cancellationToken)
    {
        var changed = await _context.EmailNotifications.Where(n => n.Id == id && n.Status == "Failed")
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.Status, "Pending")
                .SetProperty(n => n.Attempts, 0)
                .SetProperty(n => n.NextAttemptAtUtc, DateTime.UtcNow)
                .SetProperty(n => n.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(n => n.LastErrorCode, (string?)null), cancellationToken);
        if (changed == 0)
        {
            var exists = await _context.EmailNotifications.AnyAsync(n => n.Id == id, cancellationToken);
            return exists
                ? Conflict(new { message = "Only a permanently failed notification can be retried." })
                : NotFound();
        }
        return Accepted(new { message = "The confirmation notification has been queued for retry." });
    }
}

public sealed class EmailNotificationStatusDto
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public int TotalAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
}
