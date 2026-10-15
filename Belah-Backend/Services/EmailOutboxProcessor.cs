using Belah_Backend.Data;
using Belah_Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Belah_Backend.Services;

public sealed class EmailOutboxProcessor
{
    public const int MaximumAttempts = 5;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)
    };

    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _sender;
    private readonly IOptionsMonitor<PurchaseConfirmationOptions> _options;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    public EmailOutboxProcessor(ApplicationDbContext context, IEmailSender sender,
        IOptionsMonitor<PurchaseConfirmationOptions> options, ILogger<EmailOutboxProcessor> logger)
    {
        _context = context;
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    public async Task<int> ProcessAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_sender.IsConfigured) return 0;
        var now = DateTime.UtcNow;

        await _context.EmailNotifications
            .Where(n => n.Status == "Processing" && n.Attempts >= MaximumAttempts && n.LeaseExpiresAtUtc <= now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.Status, "Failed")
                .SetProperty(n => n.LastErrorCode, "DeliveryLeaseExpired")
                .SetProperty(n => n.LeaseExpiresAtUtc, (DateTime?)null), cancellationToken);

        var candidateIds = await _context.EmailNotifications.AsNoTracking()
            .Where(n => n.Attempts < MaximumAttempts &&
                ((n.Status == "Pending" && n.NextAttemptAtUtc <= now) ||
                 (n.Status == "Processing" && n.LeaseExpiresAtUtc <= now)))
            .OrderBy(n => n.NextAttemptAtUtc)
            .ThenBy(n => n.Id)
            .Select(n => n.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        foreach (var id in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            now = DateTime.UtcNow;
            var claimed = await _context.EmailNotifications.Where(n => n.Id == id && n.Attempts < MaximumAttempts &&
                    ((n.Status == "Pending" && n.NextAttemptAtUtc <= now) ||
                     (n.Status == "Processing" && n.LeaseExpiresAtUtc <= now)))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(n => n.Status, "Processing")
                    .SetProperty(n => n.Attempts, n => n.Attempts + 1)
                    .SetProperty(n => n.TotalAttempts, n => n.TotalAttempts + 1)
                    .SetProperty(n => n.LastAttemptAtUtc, now)
                    .SetProperty(n => n.LeaseExpiresAtUtc, now.Add(LeaseDuration)), cancellationToken);
            if (claimed != 1) continue;

            var notification = await _context.EmailNotifications
                .Include(n => n.Order!).ThenInclude(order => order.OrderItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
            if (notification?.Order == null)
            {
                await _context.EmailNotifications.Where(n => n.Id == id && n.Status == "Processing")
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(n => n.Status, "Failed")
                        .SetProperty(n => n.LastErrorCode, "OrderUnavailable")
                        .SetProperty(n => n.LeaseExpiresAtUtc, (DateTime?)null), cancellationToken);
                continue;
            }

            try
            {
                var message = PurchaseConfirmationTemplate.Render(notification, notification.Order, _options.CurrentValue);
                await _sender.SendAsync(message, cancellationToken);
                var sentAt = DateTime.UtcNow;
                await _context.EmailNotifications.Where(n => n.Id == id && n.Status == "Processing")
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(n => n.Status, "Sent")
                        .SetProperty(n => n.SentAtUtc, (DateTime?)sentAt)
                        .SetProperty(n => n.LeaseExpiresAtUtc, (DateTime?)null)
                        .SetProperty(n => n.LastErrorCode, (string?)null), cancellationToken);
                delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failedAt = DateTime.UtcNow;
                var permanentFailure = notification.Attempts >= MaximumAttempts;
                var retryAt = permanentFailure ? failedAt : failedAt.Add(RetryDelays[Math.Min(notification.Attempts - 1, RetryDelays.Length - 1)]);
                var errorCode = exception.GetType().Name;
                await _context.EmailNotifications.Where(n => n.Id == id && n.Status == "Processing")
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(n => n.Status, permanentFailure ? "Failed" : "Pending")
                        .SetProperty(n => n.NextAttemptAtUtc, retryAt)
                        .SetProperty(n => n.LeaseExpiresAtUtc, (DateTime?)null)
                        .SetProperty(n => n.LastErrorCode, errorCode), cancellationToken);
                _logger.LogWarning("Purchase confirmation delivery failed for notification {NotificationId} on attempt {Attempt}; error type {ErrorType}.",
                    id, notification.Attempts, errorCode);
            }
        }

        return delivered;
    }
}

public sealed class EmailOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailOutboxWorker> _logger;
    private bool _configurationWarningWritten;

    public EmailOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                if (!sender.IsConfigured)
                {
                    if (!_configurationWarningWritten)
                    {
                        _logger.LogWarning("Transactional email is not configured; confirmation notifications remain pending.");
                        _configurationWarningWritten = true;
                    }
                }
                else
                {
                    _configurationWarningWritten = false;
                    await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessAvailableAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError("Email outbox pass failed with exception type {ErrorType}; it will retry.", exception.GetType().Name);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
