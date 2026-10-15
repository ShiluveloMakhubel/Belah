using Belah_Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/reports")]
public sealed class AdminCommerceReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public AdminCommerceReportsController(ApplicationDbContext context) => _context = context;

    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory([FromQuery] int lowStockThreshold = 5, CancellationToken cancellationToken = default)
    {
        if (lowStockThreshold is < 0 or > 10000)
            return BadRequest(new { message = "Low-stock threshold must be between 0 and 10,000." });

        var products = await _context.Products.AsNoTracking().Include(p => p.Variants)
            .OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var rows = products.SelectMany(product =>
        {
            var variants = product.Variants.Where(v => v.IsActive).ToList();
            if (variants.Count == 0)
                return new[] { new InventoryRowDto(product.Id, null, product.Name ?? "Unnamed product", product.Sku,
                    product.StockQuantity, product.IsActive, product.IsStockConfirmed, product.StockQuantity <= lowStockThreshold) };

            return variants.Select(variant => new InventoryRowDto(product.Id, variant.Id,
                $"{product.Name ?? "Unnamed product"} — {variant.Name}", variant.Sku ?? product.Sku, variant.StockQuantity,
                product.IsActive, product.IsStockConfirmed, variant.StockQuantity <= lowStockThreshold));
        }).ToList();

        return Ok(new InventoryReportDto(lowStockThreshold, rows.Count, rows.Count(r => r.IsActive),
            rows.Count(r => r.IsActive && r.StockQuantity == 0),
            rows.Count(r => r.IsActive && r.StockQuantity > 0 && r.StockQuantity <= lowStockThreshold),
            rows.Count(r => r.IsActive && !r.IsStockConfirmed), rows));
    }

    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var from = fromUtc?.ToUniversalTime() ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        if (from >= to || to - from > TimeSpan.FromDays(366))
            return BadRequest(new { message = "Choose a valid date range of no more than 366 days." });

        var paidOrders = await _context.Orders.AsNoTracking().Include(o => o.OrderItems)
            .Where(o => o.PaymentStatus == "Paid" && o.OrderDate >= from && o.OrderDate < to)
            .OrderBy(o => o.OrderDate).ToListAsync(cancellationToken);
        var daily = paidOrders.GroupBy(o => o.OrderDate.Date).OrderBy(g => g.Key)
            .Select(g => new SalesDayDto(g.Key, g.Count(), g.Sum(o => o.TotalAmount))).ToList();
        var topProducts = paidOrders.SelectMany(o => o.OrderItems)
            .GroupBy(i => i.ProductName)
            .Select(g => new TopSellingProductDto(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.Subtotal)))
            .OrderByDescending(p => p.UnitsSold).ThenBy(p => p.ProductName).Take(10).ToList();
        var revenue = paidOrders.Sum(o => o.TotalAmount);

        return Ok(new SalesReportDto(from, to, paidOrders.Count, revenue,
            paidOrders.Count == 0 ? 0 : decimal.Round(revenue / paidOrders.Count, 2), daily, topProducts));
    }
}

public sealed record InventoryRowDto(int ProductId, int? VariantId, string DisplayName, string? Sku, int StockQuantity,
    bool IsActive, bool IsStockConfirmed, bool IsLowStock);
public sealed record InventoryReportDto(int LowStockThreshold, int InventoryRows, int ActiveRows, int OutOfStockRows,
    int LowStockRows, int UnconfirmedRows, IReadOnlyList<InventoryRowDto> Items);
public sealed record SalesDayDto(DateTime DateUtc, int Orders, decimal Revenue);
public sealed record TopSellingProductDto(string ProductName, int UnitsSold, decimal Revenue);
public sealed record SalesReportDto(DateTime FromUtc, DateTime ToUtc, int PaidOrders, decimal Revenue,
    decimal AverageOrderValue, IReadOnlyList<SalesDayDto> Daily, IReadOnlyList<TopSellingProductDto> TopProducts);
