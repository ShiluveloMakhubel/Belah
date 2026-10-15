using System.Security.Cryptography;
using System.Data.Common;
using System.Text.Json;
using Belah_Backend.Data;
using Belah_Backend.DTOs;
using Belah_Backend.Models;
using Belah_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Belah_Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class PaymentsController : ControllerBase
{
    private const string Currency = "ZAR";
    private readonly ApplicationDbContext _context;
    private readonly IPaystackService _paystackService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IConfiguration _configuration;

    public PaymentsController(ApplicationDbContext context, IPaystackService paystackService, ILogger<PaymentsController> logger, IConfiguration configuration)
    {
        _context = context;
        _paystackService = paystackService;
        _logger = logger;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("initialize")]
    public async Task<ActionResult<InitializePaymentResponse>> InitializePayment([FromBody] InitializePaymentRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.OrderNumber)) return BadRequest();
        var order = await _context.Orders.Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber.Trim());
        if (order == null) return NotFound(new { message = "Order not found." });
        if (!CanManagePayment(order, request.GuestCapability)) return Forbid();
        if (order.PaymentExpiresAt.HasValue && order.PaymentExpiresAt.Value <= DateTime.UtcNow)
        {
            await _context.Orders.Where(o => o.Id == order.Id && o.PaymentStatus == "Pending")
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Expired")
                    .SetProperty(o => o.Status, "Cancelled"));
            return Conflict(new { message = "This checkout expired. Create a new order to continue." });
        }
        if (order.PaymentStatus != "Pending" || order.Status == "Cancelled")
            return Conflict(new { message = "This order is not available for payment." });
        if (order.TotalAmount <= 0 || order.Email.Length == 0) return BadRequest(new { message = "Order payment details are invalid." });

        if (order.OrderItems.Count == 0 || order.OrderItems.Any(i => !i.ProductId.HasValue || i.Quantity <= 0 || i.UnitPrice < 0 || i.Subtotal != i.UnitPrice * i.Quantity) ||
            order.OrderItems.Sum(i => i.Subtotal) + order.DeliveryFee != order.TotalAmount)
            return Conflict(new { message = "Stored order lines are invalid; create a new order." });
        var lineQuantities = order.OrderItems.GroupBy(i => i.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        var products = await _context.Products.Where(p => lineQuantities.Keys.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var (productId, quantity) in lineQuantities)
        {
            if (!products.TryGetValue(productId, out var product) || !product.IsActive ||
                !product.IsPriceApproved || !product.IsStockConfirmed)
                return Conflict(new { message = "An order product is no longer available." });
            if (quantity > product.StockQuantity)
                return Conflict(new { message = "Current inventory is insufficient; create a new order later." });
        }
        var orderedVariantIds = order.OrderItems.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var currentVariants = await _context.ProductVariants.Where(v => orderedVariantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        foreach (var group in order.OrderItems.Where(i => i.ProductVariantId.HasValue).GroupBy(i => i.ProductVariantId!.Value))
        {
            if (!currentVariants.TryGetValue(group.Key, out var variant) || !variant.IsActive ||
                variant.ProductId != group.First().ProductId || group.Sum(i => (long)i.Quantity) > variant.StockQuantity)
                return Conflict(new { message = "The selected product size is no longer available." });
        }
        var productIdsWithVariants = await _context.ProductVariants.Where(v => v.IsActive && lineQuantities.Keys.Contains(v.ProductId))
            .Select(v => v.ProductId).Distinct().ToListAsync();
        if (order.OrderItems.Any(i => !i.ProductVariantId.HasValue && productIdsWithVariants.Contains(i.ProductId!.Value)))
            return Conflict(new { message = "A product size must be selected before payment." });

        // Reference is persisted before the provider call, so every callback resolves to one order.
        if (string.IsNullOrWhiteSpace(order.PaymentReference))
        {
            var newReference = CreatePaymentReference();
            var changed = await _context.Orders.Where(o => o.Id == order.Id && o.PaymentStatus == "Pending" && o.PaymentReference == null)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentReference, newReference));
            order = await _context.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
            if (changed == 1 && order.PaymentReference != newReference)
                return Conflict(new { message = "Unable to initialize payment. Please retry." });
        }

        long minorUnits;
        try { minorUnits = checked((long)(order.TotalAmount * 100m)); }
        catch (OverflowException) { return BadRequest(new { message = "Order amount is outside the supported range." }); }

        var provider = await _paystackService.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = order.Email,
            Amount = minorUnits,
            Currency = Currency,
            Reference = order.PaymentReference!
        });
        if (provider == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Payment provider is unavailable or payment configuration is incomplete." });

        return Ok(new InitializePaymentResponse
        {
            Success = true,
            Message = "Payment session initialized.",
            OrderNumber = order.OrderNumber,
            PaymentReference = order.PaymentReference!,
            Amount = order.TotalAmount,
            AmountInMinorUnits = minorUnits,
            Currency = Currency,
            CustomerEmail = order.Email,
            AuthorizationUrl = provider.AuthorizationUrl,
            AccessCode = provider.AccessCode
        });
    }

    [AllowAnonymous]
    [HttpPost("verify")]
    public async Task<ActionResult<VerifyPaymentResponse>> VerifyPayment([FromBody] VerifyPaymentRequest request)
        => await VerifyPaymentCore(request, enforceOwnership: true);

    [AllowAnonymous]
    [HttpPost("webhook")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> HandlePaystackWebhook(CancellationToken cancellationToken)
    {
        var secret = _configuration["Paystack:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("Paystack webhook received while payment secret is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (body.Length + bytesRead > 64 * 1024)
                return BadRequest(new { message = "Webhook payload is missing or too large." });
            await body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
        if (body.Length == 0 || body.Length > 64 * 1024)
            return BadRequest(new { message = "Webhook payload is missing or too large." });
        var payload = body.ToArray();
        var suppliedSignature = Request.Headers["x-paystack-signature"].ToString();
        if (!IsValidWebhookSignature(payload, suppliedSignature, secret))
            return Unauthorized();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Webhook payload is not valid JSON." });
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return BadRequest(new { message = "Webhook payload must be a JSON object." });
            if (!root.TryGetProperty("event", out var eventElement) || eventElement.ValueKind != JsonValueKind.String)
                return BadRequest(new { message = "Webhook event type is missing." });
            if (!string.Equals(eventElement.GetString(), "charge.success", StringComparison.Ordinal))
                return Ok();
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("reference", out var referenceElement) ||
                referenceElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(referenceElement.GetString()))
                return BadRequest(new { message = "Successful payment event has no transaction reference." });

            var result = await VerifyPaymentCore(new VerifyPaymentRequest { Reference = referenceElement.GetString()! }, enforceOwnership: false);
            return result.Result ?? Ok(result.Value);
        }
    }

    internal static bool IsValidWebhookSignature(byte[] payload, string? suppliedSignature, string secret)
    {
        if (payload.Length == 0 || string.IsNullOrWhiteSpace(suppliedSignature) || string.IsNullOrEmpty(secret)) return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(suppliedSignature); }
        catch (FormatException) { return false; }
        var expected = HMACSHA512.HashData(System.Text.Encoding.UTF8.GetBytes(secret), payload);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private async Task<ActionResult<VerifyPaymentResponse>> VerifyPaymentCore(VerifyPaymentRequest request, bool enforceOwnership)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reference)) return BadRequest();
        var reference = request.Reference.Trim();
        var order = await _context.Orders.Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.PaymentReference == reference);
        if (order == null) return NotFound(new { message = "Payment reference is unknown." });
        if (enforceOwnership && !CanManagePayment(order, request.GuestCapability)) return Forbid();
        if (!string.Equals(order.PaymentReference, reference, StringComparison.Ordinal))
            return BadRequest(new { message = "Payment reference does not match the order." });

        // This fast path is valid only because both the exact reference and provider transaction ID were persisted.
        if (order.PaymentStatus == "Paid" && order.PaystackTransactionId.HasValue)
            return Ok(ToResponse(order, reference));
        if (order.PaymentStatus == "RefundRequired" && order.PaystackTransactionId.HasValue)
            return Conflict(new { message = "Payment succeeded but inventory could not be fulfilled; refund handling is required." });

        var provider = await _paystackService.VerifyTransactionAsync(reference);
        if (provider == null) return StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { message = "Payment verification is temporarily unavailable." });
        if (!string.Equals(provider.Reference, reference, StringComparison.Ordinal))
            return BadRequest(new { message = "Provider transaction reference mismatch." });
        if (!string.Equals(provider.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(provider.Status, "failed", StringComparison.OrdinalIgnoreCase))
                await _context.Orders.Where(o => o.Id == order.Id && o.PaymentReference == reference && o.PaymentStatus == "Pending")
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Failed"));
            return Conflict(new { message = "Payment has not succeeded." });
        }
        if (provider.Id <= 0) return BadRequest(new { message = "Provider transaction identifier is invalid." });
        if (await _context.Orders.AnyAsync(o => o.PaystackTransactionId == provider.Id && o.Id != order.Id))
            return Conflict(new { message = "This provider transaction is already associated with another order." });
        if (provider.Amount != checked((long)(order.TotalAmount * 100m)) ||
            !string.Equals(provider.Currency, Currency, StringComparison.OrdinalIgnoreCase) ||
            provider.Customer == null || !string.Equals(provider.Customer.Email, order.Email, StringComparison.OrdinalIgnoreCase))
        {
            await RecordRefundRequired(order.Id, reference, provider.Id);
            return Conflict(new { message = "Provider reported a successful but mismatched payment; refund review is required." });
        }
        if ((order.PaymentExpiresAt.HasValue && order.PaymentExpiresAt.Value <= DateTime.UtcNow) ||
            order.Status == "Cancelled" || order.PaymentStatus == "Expired")
        {
            await RecordRefundRequired(order.Id, reference, provider.Id);
            return Conflict(new { message = "Payment succeeded after checkout expired; refund review is required." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var claimed = await _context.Orders.Where(o => o.Id == order.Id && o.PaymentReference == reference &&
                    (o.PaymentStatus == "Pending" || o.PaymentStatus == "Failed") && o.Status != "Cancelled" &&
                    (!o.PaymentExpiresAt.HasValue || o.PaymentExpiresAt > DateTime.UtcNow))
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Verifying"));
            if (claimed != 1)
            {
                await transaction.RollbackAsync();
                if (order.Status == "Cancelled" && (order.PaymentStatus == "Pending" || order.PaymentStatus == "Failed"))
                {
                    await RecordRefundRequired(order.Id, reference, provider.Id);
                    return Conflict(new { message = "Payment succeeded after the order was cancelled; refund review is required." });
                }
                return await AlreadyProcessedOrConflict(order.Id, reference, provider.Id);
            }

            var invalidProduct = order.OrderItems.Any(i => !i.ProductId.HasValue || i.Quantity <= 0);
            var inventoryOk = !invalidProduct;
            var productQuantities = order.OrderItems.Where(i => i.ProductId.HasValue)
                .GroupBy(i => i.ProductId!.Value).OrderBy(g => g.Key)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => (long)i.Quantity) }).ToList();
            var variantQuantities = order.OrderItems.Where(i => i.ProductVariantId.HasValue)
                .GroupBy(i => i.ProductVariantId!.Value).OrderBy(g => g.Key)
                .Select(g => new { VariantId = g.Key, ProductId = g.First().ProductId!.Value, Quantity = g.Sum(i => (long)i.Quantity) }).ToList();
            foreach (var item in variantQuantities)
            {
                if (item.Quantity > int.MaxValue) { inventoryOk = false; break; }
                var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId &&
                    p.IsActive && p.IsPriceApproved && p.IsStockConfirmed);
                var affected = product == null ? 0 : await _context.ProductVariants
                    .Where(v => v.Id == item.VariantId && v.ProductId == item.ProductId && v.IsActive &&
                        v.StockQuantity >= item.Quantity && _context.Products.Any(p => p.Id == v.ProductId &&
                            p.IsActive && p.IsPriceApproved && p.IsStockConfirmed))
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - (int)item.Quantity));
                if (affected != 1) { inventoryOk = false; break; }
            }
            foreach (var item in productQuantities.Where(q => !order.OrderItems.Any(i => i.ProductId == q.ProductId && i.ProductVariantId.HasValue)))
            {
                if (item.Quantity > int.MaxValue) { inventoryOk = false; break; }
                var affected = await _context.Products.Where(p => p.Id == item.ProductId && p.IsActive &&
                        p.IsPriceApproved && p.IsStockConfirmed &&
                        !_context.ProductVariants.Any(v => v.ProductId == p.Id && v.IsActive) && p.StockQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - (int)item.Quantity));
                if (affected != 1) { inventoryOk = false; break; }
            }
            foreach (var item in productQuantities.Where(q => order.OrderItems.Any(i => i.ProductId == q.ProductId && i.ProductVariantId.HasValue)))
            {
                if (item.Quantity > int.MaxValue) { inventoryOk = false; break; }
                var affected = await _context.Products.Where(p => p.Id == item.ProductId && p.IsActive &&
                        p.IsPriceApproved && p.IsStockConfirmed && p.StockQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - (int)item.Quantity));
                if (affected != 1) { inventoryOk = false; break; }
            }

            if (!inventoryOk)
            {
                await transaction.RollbackAsync();
                await _context.Orders.Where(o => o.Id == order.Id && o.PaymentReference == reference &&
                        (o.PaymentStatus == "Pending" || o.PaymentStatus == "Failed"))
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "RefundRequired")
                        .SetProperty(o => o.Status, "RefundRequired")
                        .SetProperty(o => o.PaystackTransactionId, (long?)provider.Id));
                _logger.LogError("Verified payment requires manual refund handling for order {OrderId}.", order.Id);
                return Conflict(new { message = "Payment succeeded but inventory was unavailable; refund handling is required." });
            }

            var paid = await _context.Orders.Where(o => o.Id == order.Id && o.PaymentStatus == "Verifying" && o.PaymentReference == reference)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Paid")
                    .SetProperty(o => o.Status, "Processing")
                    .SetProperty(o => o.PaystackTransactionId, (long?)provider.Id));
            if (paid != 1) throw new DbUpdateConcurrencyException("Order payment state changed during verification.");

            // The unique outbox key and the Paid transition commit together. Existing paid orders are
            // intentionally untouched; only this newly confirmed transition enqueues a message.
            _context.EmailNotifications.Add(new EmailNotification
            {
                OrderId = order.Id,
                IdempotencyKey = $"order-confirmation:{order.Id}",
                RecipientEmail = order.Email,
                Currency = Currency,
                Status = "Pending",
                Attempts = 0,
                CreatedAtUtc = DateTime.UtcNow,
                NextAttemptAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            order.PaymentStatus = "Paid";
            order.Status = "Processing";
            order.PaystackTransactionId = provider.Id;
            return Ok(ToResponse(order, reference));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning(ex, "Concurrent payment state change for order {OrderId}.", order.Id);
            return await AlreadyProcessedOrConflict(order.Id, reference, provider.Id);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning(ex, "Payment state conflict for order {OrderId}.", order.Id);
            return Conflict(new { message = "Payment was already processed or conflicts with another order." });
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Database allocation failed after provider success for order {OrderId}.", order.Id);
            try { await RecordRefundRequired(order.Id, reference, provider.Id); }
            catch (DbException) { /* Database unavailable; pending payment requires operational reconciliation. */ }
            return await AlreadyProcessedOrConflict(order.Id, reference, provider.Id);
        }
    }

    private bool CanManagePayment(Order order, string? capability)
    {
        if (User.IsInRole("Admin")) return true;
        if (order.UserId.HasValue)
            return int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) && id == order.UserId.Value;
        return OrdersController.IsCapabilityValid(order, capability);
    }

    private async Task<ActionResult<VerifyPaymentResponse>> AlreadyProcessedOrConflict(int orderId, string reference, long transactionId)
    {
        var current = await _context.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
        if (current.PaymentReference == reference && current.PaymentStatus == "Paid" && current.PaystackTransactionId == transactionId)
            return Ok(ToResponse(current, reference));
        if (current.PaymentReference == reference && current.PaymentStatus == "RefundRequired" && current.PaystackTransactionId == transactionId)
            return Conflict(new { message = "Payment succeeded but inventory could not be fulfilled; refund handling is required." });
        return Conflict(new { message = "Payment is being processed or is not eligible for payment." });
    }

    private Task<int> RecordRefundRequired(int orderId, string reference, long transactionId) =>
        _context.Orders.Where(o => o.Id == orderId && o.PaymentReference == reference &&
                (o.PaymentStatus == "Pending" || o.PaymentStatus == "Failed" || o.PaymentStatus == "Expired"))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "RefundRequired")
                .SetProperty(o => o.Status, "RefundRequired")
                .SetProperty(o => o.PaystackTransactionId, (long?)transactionId));

    private static VerifyPaymentResponse ToResponse(Order order, string reference) => new()
    {
        Success = true, Message = "Payment verified successfully.", OrderNumber = order.OrderNumber,
        PaymentStatus = order.PaymentStatus, OrderStatus = order.Status,
        Amount = order.TotalAmount, Reference = reference
    };

    private static string CreatePaymentReference() =>
        "BELAH-" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
