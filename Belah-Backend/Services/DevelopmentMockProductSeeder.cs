using Belah_Backend.Data;
using Belah_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Services;

public sealed class DevelopmentMockProductSeeder
{
    public const string SkuPrefix = "BELAH-DEV-MOCK-";
    public const string SampleMarker = "Development sample for catalogue testing; not actual inventory.";

    private static readonly Product[] Catalogue =
    {
        Product("001", "Body Wave Lace Front Wig · 16 inch", "Wigs", 4299m, 7, "/images/hair.webp", "Body wave style; 13×4 lace; 180% density; natural black.", "Sample listing for a shoulder-length body wave look."),
        Product("002", "Body Wave Lace Front Wig · 20 inch", "Wigs", 5199m, 5, "/images/hair.webp", "Body wave style; 13×4 lace; 180% density; natural black.", "Sample listing for a longer body wave look."),
        Product("003", "Silky Straight Lace Front Wig · 18 inch", "Wigs", 4699m, 6, "/images/hair2.png", "Straight style; 13×4 lace; 180% density; natural black.", "Sample listing for a sleek straight look."),
        Product("004", "Deep Wave Lace Front Wig · 20 inch", "Wigs", 5399m, 4, "/images/curly.webp", "Deep wave style; 13×4 lace; 180% density; natural black.", "Sample listing with a defined deep wave pattern."),
        Product("005", "Curly Bob Closure Wig · 12 inch", "Wigs", 3599m, 8, "/images/curly.webp", "Curly bob style; 5×5 closure; natural black.", "Sample listing for a shorter curly style."),
        Product("006", "Body Wave Bundle · 18 inch", "Bundles", 1399m, 12, "/images/hair.webp", "Body wave texture; 18 inch; one bundle per item; natural black.", "Sample listing for a body wave bundle."),
        Product("007", "Silky Straight Bundle · 20 inch", "Bundles", 1499m, 10, "/images/hair2.png", "Straight texture; 20 inch; one bundle per item; natural black.", "Sample listing for a straight bundle."),
        Product("008", "Deep Wave Bundle · 22 inch", "Bundles", 1699m, 9, "/images/curly.webp", "Deep wave texture; 22 inch; one bundle per item; natural black.", "Sample listing for a deep wave bundle."),
        Product("009", "HD Lace Closure · 4×4", "Closures", 799m, 11, "/images/hair2.png", "4×4 lace area; natural black; middle or side parting.", "Sample listing for a compact lace closure."),
        Product("010", "HD Lace Closure · 5×5", "Closures", 999m, 8, "/images/hair.webp", "5×5 lace area; natural black; middle or side parting.", "Sample listing for a larger lace closure."),
        Product("011", "HD Lace Frontal · 13×4", "Frontals", 1799m, 7, "/images/hair.webp", "13×4 lace area; natural black; ear-to-ear coverage.", "Sample listing for a 13×4 lace frontal."),
        Product("012", "HD Lace Frontal · 13×6", "Frontals", 2099m, 5, "/images/hair2.png", "13×6 lace area; natural black; deeper parting space.", "Sample listing for a 13×6 lace frontal."),
        Product("013", "Satin Hair Wrap", "Hair Care & Accessories", 189m, 24, "/images/curly.webp", "Soft satin wrap; adjustable tie; one size.", "Sample accessory listing for overnight hair wrapping."),
        Product("014", "Wig Grip Band", "Hair Care & Accessories", 149m, 20, "/images/hair2.png", "Adjustable non-slip band; one size; dark brown.", "Sample accessory listing for wig styling.")
    };

    public static void EnsureDevelopmentEnvironment(string environmentName)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mock catalogue seeding is available only when ASPNETCORE_ENVIRONMENT is Development.");
    }

    public async Task<SeedResult> SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var seedSkus = Catalogue.Select(p => p.Sku!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingProducts = await context.Products.Where(p => p.Sku != null && seedSkus.Contains(p.Sku))
            .ToListAsync(cancellationToken);
        var existingBySku = existingProducts.GroupBy(p => p.Sku!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;
        var skipped = 0;
        foreach (var seed in Catalogue)
        {
            if (!existingBySku.TryGetValue(seed.Sku!, out var matches))
            {
                context.Products.Add(CloneSeed(seed));
                added++;
                continue;
            }

            // Reserved SKU alone is insufficient evidence that a row belongs to this seeder.
            // Only update a row that carries the visible development sample marker.
            if (matches.Count != 1 || matches[0].Description?.StartsWith(SampleMarker, StringComparison.Ordinal) != true)
            {
                skipped++;
                continue;
            }

            CopySeedFields(seed, matches[0]);
            updated++;
        }

        if (added > 0 || updated > 0) await context.SaveChangesAsync(cancellationToken);
        return new SeedResult(Catalogue.Length, added, updated, skipped);
    }

    private static Product Product(string sequence, string name, string category, decimal price, int stock,
        string imageUrl, string specification, string details) => new()
    {
        Sku = $"{SkuPrefix}{sequence}",
        Name = name,
        Description = $"{SampleMarker} {details}",
        Category = category,
        Price = price,
        StockQuantity = stock,
        ImageUrl = imageUrl,
        Specification = specification,
        IsActive = true
    };

    private static void CopySeedFields(Product source, Product target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.Category = source.Category;
        target.Price = source.Price;
        target.StockQuantity = source.StockQuantity;
        target.ImageUrl = source.ImageUrl;
        target.Specification = source.Specification;
        target.IsActive = source.IsActive;
    }

    private static Product CloneSeed(Product source) => new()
    {
        Sku = source.Sku, Name = source.Name, Description = source.Description, Category = source.Category,
        Price = source.Price, StockQuantity = source.StockQuantity, ImageUrl = source.ImageUrl,
        Specification = source.Specification, IsActive = source.IsActive
    };

    public sealed record SeedResult(int Total, int Added, int Updated, int SkippedConflicts);
}
