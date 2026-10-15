using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Belah_Backend.Data;
using Belah_Backend.DTOs;
using Belah_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class OrdersController : ControllerBase
{
    public const decimal FixedDeliveryFee = 99m;
    private readonly ApplicationDbContext _context;

    private static readonly string[] AllowedOrderStatuses =
        { "Pending", "Processing", "Shipped", "Completed", "Cancelled", "RefundRequired" };

    public OrdersController(ApplicationDbContext context) => _context = context;

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<CreateOrderResponseDto>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid || request.Items == null || request.Items.Count == 0)
            return BadRequest(new { message = "A valid order with at least one item is required." });

        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Address) ||
            string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.Province) ||
            string.IsNullOrWhiteSpace(request.PostalCode))
            return BadRequest(new { message = "All customer and delivery fields are required." });

        if (request.Items.Any(i => i.ProductId <= 0 || i.Quantity <= 0 || i.VariantId is <= 0))
            return BadRequest(new { message = "Each order item must have a valid product and positive quantity." });

        var quantities = request.Items.GroupBy(i => new { i.ProductId, i.VariantId })
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        if (quantities.Values.Any(q => q > int.MaxValue))
            return BadRequest(new { message = "Requested quantity is too large." });

        var products = await _context.Products
            .Where(p => quantities.Keys.Select(k => k.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        if (products.Count != quantities.Keys.Select(k => k.ProductId).Distinct().Count())
            return BadRequest(new { message = "One or more products do not exist." });

        var variantIds = quantities.Keys.Where(k => k.VariantId.HasValue).Select(k => k.VariantId!.Value).Distinct().ToList();
        var variants = await _context.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        if (variants.Count != variantIds.Count) return BadRequest(new { message = "One or more product sizes are unavailable." });

        foreach (var (line, quantity) in quantities)
        {
            var product = products[line.ProductId];
            if (!product.IsActive)
                return BadRequest(new { message = $"Product '{product.Name}' is unavailable." });
            if (!product.IsPriceApproved)
                return Conflict(new { message = $"The price for '{product.Name}' is pending approval and it cannot be purchased yet." });
            if (!product.IsStockConfirmed)
                return Conflict(new { message = $"Availability for '{product.Name}' has not been confirmed and it cannot be purchased yet." });
            if (line.VariantId.HasValue)
            {
                var variant = variants[line.VariantId.Value];
                if (variant.ProductId != product.Id || !variant.IsActive)
                    return BadRequest(new { message = "The selected product size is unavailable." });
                if (quantity > variant.StockQuantity)
                    return Conflict(new { message = $"Insufficient stock for '{product.Name}' ({variant.Name})." });
            }
            else if (await _context.ProductVariants.AnyAsync(v => v.ProductId == product.Id && v.IsActive))
                return BadRequest(new { message = $"Select a size or option for '{product.Name}'." });
            else if (quantity > product.StockQuantity)
                return Conflict(new { message = $"Insufficient stock for '{product.Name}'." });
        }

        int? userId = null;
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
                return Unauthorized();
            if (!await _context.Users.AnyAsync(u => u.Id == parsedUserId)) return Unauthorized();
            userId = parsedUserId;
        }

        var orderItems = new List<OrderItem>();
        decimal subtotalAmount = 0;
        foreach (var line in request.Items)
        {
            var product = products[line.ProductId];
            var variant = line.VariantId.HasValue ? variants[line.VariantId.Value] : null;
            var unitPrice = variant?.SalePrice ?? variant?.Price ?? product.SalePrice ?? product.Price;
            var subtotal = unitPrice * line.Quantity;
            subtotalAmount += subtotal;
            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                SelectedVariantName = variant?.Name,
                ProductName = product.Name ?? "Unnamed product",
                UnitPrice = unitPrice,
                Quantity = line.Quantity,
                Subtotal = subtotal,
                SelectedColor = string.IsNullOrWhiteSpace(line.SelectedColor) ? null : line.SelectedColor.Trim(),
                SelectedStorage = string.IsNullOrWhiteSpace(line.SelectedStorage) ? null : line.SelectedStorage.Trim()
            });
        }
        var total = subtotalAmount + FixedDeliveryFee;
        if (subtotalAmount <= 0 || total > 10000000m)
            return BadRequest(new { message = "Order total is outside the supported range." });

        string? guestCapability = userId.HasValue ? null : CreateGuestCapability();
        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            GuestCapabilityHash = guestCapability == null ? null : HashCapability(guestCapability),
            OrderDate = DateTime.UtcNow,
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = total,
            DeliveryFee = FixedDeliveryFee,
            CustomerName = request.CustomerName!.Trim(),
            Email = request.Email!.Trim().ToLowerInvariant(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            Province = request.Province.Trim(),
            PostalCode = request.PostalCode.Trim(),
            OrderItems = orderItems
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrderByNumber), new { orderNumber = order.OrderNumber },
            new CreateOrderResponseDto
            {
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                DeliveryFee = order.DeliveryFee,
                SubtotalAmount = subtotalAmount,
                GuestCapability = guestCapability
            });
    }

    [AllowAnonymous]
    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> GetOrderByNumber(string orderNumber)
    {
        var order = await _context.Orders.Include(o => o.OrderItems).AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        if (order == null) return NotFound(new { message = "Order not found." });
        var isAccountOwner = order.UserId.HasValue &&
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) && currentUserId == order.UserId.Value;
        var guestCapability = Request.Headers["X-Order-Capability"].FirstOrDefault();
        if (User.IsInRole("Admin") || isAccountOwner || IsCapabilityValid(order, guestCapability))
            return Ok(MapToOrderResponseDto(order));
        return Ok(MapToPublicOrderTrackingDto(order));
    }

    [Authorize]
    [HttpGet("my-orders")]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetMyOrders()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var orders = await _context.Orders.Include(o => o.OrderItems).Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate).AsNoTracking().ToListAsync();
        return Ok(orders.Select(MapToOrderResponseDto).ToList());
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders()
    {
        var orders = await _context.Orders.Include(o => o.OrderItems).OrderByDescending(o => o.OrderDate)
            .AsNoTracking().ToListAsync();
        return Ok(orders.Select(MapToOrderResponseDto).ToList());
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Status)) return BadRequest();
        if (!string.IsNullOrWhiteSpace(dto.PaymentStatus))
            return BadRequest(new { message = "Payment status is managed by verified provider transactions." });
        var status = AllowedOrderStatuses.FirstOrDefault(s => s.Equals(dto.Status, StringComparison.OrdinalIgnoreCase));
        if (status == null) return BadRequest(new { message = "Invalid order status." });
        var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (order.Status == status)
            return Ok(MapToOrderResponseDto(order));
        if (status == "Processing" && order.PaymentStatus != "Paid")
            return Conflict(new { message = "Only a verified paid order can be processed." });
        if (status is "Shipped" or "Completed" && order.PaymentStatus != "Paid")
            return Conflict(new { message = "Only a verified paid order can be fulfilled." });
        var allowedNext = order.Status switch
        {
            "Pending" => new[] { "Processing", "Cancelled" },
            "Processing" => new[] { "Shipped", "Cancelled" },
            "Shipped" => new[] { "Completed" },
            "RefundRequired" => Array.Empty<string>(),
            "Completed" or "Cancelled" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };
        if (!allowedNext.Contains(status, StringComparer.OrdinalIgnoreCase))
            return Conflict(new { message = $"Fulfilment cannot move from {order.Status} to {status}." });
        if (status == "RefundRequired" && order.PaymentStatus != "RefundRequired")
            return Conflict(new { message = "RefundRequired is set by verified payment handling and cannot be assigned manually." });
        if (order.PaymentStatus == "Paid" && status == "Cancelled")
            return Conflict(new { message = "Paid orders require a refund workflow before cancellation." });
        order.Status = status;
        await _context.SaveChangesAsync();
        return Ok(MapToOrderResponseDto(order));
    }

    internal static string CreateGuestCapability() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string HashCapability(string capability) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(capability)));

    internal static bool IsCapabilityValid(Order order, string? capability)
    {
        if (order.UserId.HasValue || string.IsNullOrWhiteSpace(capability) || string.IsNullOrWhiteSpace(order.GuestCapabilityHash)) return false;
        var supplied = Convert.FromHexString(HashCapability(capability));
        var expected = Convert.FromHexString(order.GuestCapabilityHash);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    internal bool CanManagePayment(Order order, string? guestCapability)
    {
        if (User.IsInRole("Admin")) return true;
        if (order.UserId.HasValue)
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id == order.UserId.Value;
        return IsCapabilityValid(order, guestCapability);
    }

    internal static string GenerateOrderNumber() =>
        $"BELAH-{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

    internal static OrderResponseDto MapToOrderResponseDto(Order order) => new()
    {
        OrderId = order.Id, OrderNumber = order.OrderNumber, OrderDate = order.OrderDate,
        Status = order.Status, PaymentStatus = order.PaymentStatus, TotalAmount = order.TotalAmount, DeliveryFee = order.DeliveryFee,
        SubtotalAmount = order.OrderItems.Sum(oi => oi.Subtotal),
        PaymentReference = order.PaymentReference,
        CustomerName = order.CustomerName, Email = order.Email, Phone = order.Phone,
        Address = order.Address, City = order.City, Province = order.Province, PostalCode = order.PostalCode,
        Items = order.OrderItems.Select(oi => new OrderItemResponseDto
        {
            Id = oi.Id, ProductId = oi.ProductId, ProductName = oi.ProductName,
            ProductVariantId = oi.ProductVariantId, SelectedVariantName = oi.SelectedVariantName,
            Quantity = oi.Quantity, UnitPrice = oi.UnitPrice, Subtotal = oi.Subtotal,
            SelectedColor = oi.SelectedColor, SelectedStorage = oi.SelectedStorage
        }).ToList()
    };

    private static PublicOrderTrackingDto MapToPublicOrderTrackingDto(Order order) => new()
    {
        OrderNumber = order.OrderNumber, OrderDate = order.OrderDate, Status = order.Status,
        PaymentStatus = order.PaymentStatus, TotalAmount = order.TotalAmount, DeliveryFee = order.DeliveryFee,
        Items = order.OrderItems.Select(oi => new PublicOrderItemTrackingDto
        {
            ProductName = oi.ProductName, SelectedVariantName = oi.SelectedVariantName, Quantity = oi.Quantity, UnitPrice = oi.UnitPrice,
            Subtotal = oi.Subtotal, SelectedColor = oi.SelectedColor, SelectedStorage = oi.SelectedStorage
        }).ToList()
    };
}
