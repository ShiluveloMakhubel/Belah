using Belah_Backend.Data;
using Belah_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Services;

/// <summary>Seeds non-purchasable fragrance intake listings; it never deletes or updates existing products.</summary>
public sealed class FragranceCatalogueSeeder
{
    public const string SkuPrefix = "BELAH-FRAGRANCE-INTAKE-";
    public const string IntakeMarker = "Belah fragrance intake listing. Not approved for sale.";
    public const int ExpectedCount = 10;
    public const string CategoryName = "Fragrances";

    public static IReadOnlyList<FragranceListing> Catalogue { get; } = new[]
    {
        new FragranceListing("001", "Oud Stallion Cologne", "Maison Crivelli", "Intake review required",
            "The supplied name says Cologne. Manufacturer references identify Oud Stallion as Extrait de parfum; confirm the physical bottle and size before sale.", 4999m,
            "Indicative South African reference: Verbe Activ lists Extrait de Parfum 50 ml at R5,000; size and exact stock identity are not confirmed.", "https://verbe.co.za/products/maison-crivelli-oud-stallion-extrait-de-parfum-50ml"),
        new FragranceListing("002", "Parfums de Marly Paris", "Parfums de Marly", "Identity unconfirmed",
            "The supplied entry names the fragrance house, not a specific scent. Confirm the exact fragrance and bottle size.", 0m,
            "No comparable price assigned because the fragrance is unidentified.", "https://parfums-de-marly.com/collections/all-fragrances"),
        new FragranceListing("003", "Amouage", "Amouage", "Identity unconfirmed",
            "The supplied entry names the fragrance house, not a specific scent. Confirm the exact fragrance, concentration, and bottle size.", 0m,
            "No comparable price assigned because the fragrance is unidentified.", "https://amouage.com/collections/perfumes"),
        new FragranceListing("004", "Bvlgari Aqva Pour Homme", "Bvlgari", "Eau de Toilette — reference; verify bottle",
            "The manufacturer identifies AQVA pour Homme as Eau de Toilette. Confirm that the physical bottle and size match this reference.", 2295m,
            "Indicative South African reference: Ascot Cosmetics lists the 100 ml EDT at R2,295; the Belah bottle size is unconfirmed.", "https://ascotcosmetics.co.za/product/bvlgari-aqva-pour-homme-eau-de-toilette-spray-100ml/"),
        new FragranceListing("005", "Bvlgari Omnia", "Bvlgari", "Identity unconfirmed",
            "Omnia is a collection with multiple distinct fragrances. Confirm the exact Omnia scent, concentration, and bottle size.", 0m,
            "No comparable price assigned because the fragrance variant is unidentified.", "https://www.bulgari.com/en-us/fragrances/omnia"),
        new FragranceListing("006", "Pink Sapphire", null, "Identity unconfirmed",
            "The supplied name does not identify a manufacturer. Confirm the brand, exact product, concentration, and bottle size.", 0m,
            "No comparable price assigned because the manufacturer/product match is unresolved.", "https://www.truworths.co.za/pink-sapphire-edt/product/prod1654460"),
        new FragranceListing("007", "Maison Margiela Paris", "Maison Margiela", "Identity unconfirmed",
            "The supplied entry names a fragrance house and location, not a specific scent. Confirm the fragrance and bottle size.", 0m,
            "No comparable price assigned because the fragrance is unidentified.", "https://www.maisonmargiela.com/en-us/maison-margiela/women/fragrances/perfumes/"),
        new FragranceListing("008", "Valentina Eau de Parfum", "Valentino", "Eau de Parfum — reference; verify bottle",
            "The supplied entry identifies Valentina Eau de Parfum. Confirm the physical bottle and size before sale.", 1895m,
            "Indicative South African reference: PriceCheck lists Valentino Valentina EDP 80 ml from R1,895; size and current Belah selling price require approval.", "https://www.pricecheck.co.za/categories/186/Perfumes?brand=valentino"),
        new FragranceListing("009", "Sì Passione Eau de Parfum", "Giorgio Armani", "Eau de Parfum — reference; verify bottle",
            "The supplied entry identifies Sì Passione Eau de Parfum. Confirm packaging/version and size before sale.", 1800m,
            "Indicative South African reference: Woolworths lists Sì Passione EDP from R1,800 in 30, 50, and 100 ml sizes; Belah size and selling price require approval.", "https://www.woolworths.co.za/cat/Beauty/Armani/Women-s-Fragrance/_/N-0Zybkjf6"),
        new FragranceListing("010", "Coach New York Eau de Parfum", "Coach", "Eau de Parfum — reference; verify bottle",
            "The supplied entry identifies Coach Eau de Parfum inspired by New York. Confirm the physical bottle and size before sale.", 2260m,
            "Indicative South African reference: Clicks lists Coach Eau de Parfum 90 ml at R2,260; Belah bottle size and selling price require approval.", "https://clicks.co.za/coach_eau-de-parfum-90ml/p/272166")
    };

    public static void EnsureDevelopmentEnvironment(string environmentName)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fragrance catalogue intake seeding is available only when ASPNETCORE_ENVIRONMENT is Development.");
    }

    public async Task<SeedResult> SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var category = await context.ProductCategories.FirstOrDefaultAsync(c => c.Slug == "fragrances", cancellationToken);
        if (category == null)
        {
            category = new ProductCategory { Name = CategoryName, Slug = "fragrances", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
            context.ProductCategories.Add(category);
            await context.SaveChangesAsync(cancellationToken);
        }

        var skus = Catalogue.Select(item => SkuPrefix + item.Sequence).ToArray();
        var names = Catalogue.Select(item => item.Name).ToArray();
        var existingSkus = await context.Products.Where(p => p.Sku != null && skus.Contains(p.Sku))
            .Select(p => p.Sku!).ToListAsync(cancellationToken);
        var existingNames = await context.Products.AsNoTracking()
            .Where(p => names.Contains(p.Name!))
            .Select(p => p.Name).ToListAsync(cancellationToken);

        var existingSkuSet = existingSkus.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var skipped = 0;
        foreach (var item in Catalogue)
        {
            var sku = SkuPrefix + item.Sequence;
            var sameProductExists = existingNames.Any(existing =>
                string.Equals(existing, item.Name, StringComparison.OrdinalIgnoreCase));
            if (existingSkuSet.Contains(sku) || sameProductExists)
            {
                skipped++;
                continue;
            }

            context.Products.Add(new Product
            {
                Sku = sku,
                Name = item.Name,
                Brand = item.Brand,
                ProductType = item.ProductType,
                Category = CategoryName,
                CategoryId = category.Id,
                Description = $"{IntakeMarker} {item.Description}",
                Specification = $"{item.PriceNote} Reference: {item.PriceSourceUrl}",
                ImageUrl = "/images/product-placeholder.svg",
                Price = item.ReferencePrice,
                StockQuantity = 0,
                IsPriceApproved = false,
                IsStockConfirmed = false,
                IsActive = true,
                IsFeatured = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            added++;
        }

        if (added > 0) await context.SaveChangesAsync(cancellationToken);
        return new SeedResult(Catalogue.Count, added, skipped);
    }

    public sealed record FragranceListing(string Sequence, string Name, string? Brand, string ProductType,
        string Description, decimal ReferencePrice, string PriceNote, string PriceSourceUrl);

    public sealed record SeedResult(int Total, int Added, int SkippedExisting);
}
