using Belah_Backend.Data;
using Belah_Backend.Models;
using Belah_Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Tests;

public sealed class FragranceCatalogueSeederTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"FragranceIntake_{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task Seed_CreatesAllIntakeRowsWithStableSkusAndBlocksPurchasability()
    {
        using var context = CreateContext();
        var seeder = new FragranceCatalogueSeeder();

        var result = await seeder.SeedAsync(context);
        var products = await context.Products.OrderBy(p => p.Sku).ToListAsync();

        Assert.Equal(10, result.Total);
        Assert.Equal(10, result.Added);
        Assert.Equal(0, result.SkippedExisting);
        Assert.Equal(10, products.Count);
        Assert.Equal(10, products.Select(p => p.Sku).Distinct().Count());
        Assert.All(products, product =>
        {
            Assert.StartsWith(FragranceCatalogueSeeder.SkuPrefix, product.Sku);
            Assert.Equal("Fragrances", product.Category);
            Assert.Equal("/images/product-placeholder.svg", product.ImageUrl);
            Assert.Equal(0, product.StockQuantity);
            Assert.False(product.IsStockConfirmed);
            Assert.False(product.IsPriceApproved);
            Assert.True(product.IsActive);
            Assert.StartsWith(FragranceCatalogueSeeder.IntakeMarker, product.Description);
        });
        Assert.Single(await context.ProductCategories.ToListAsync());
    }

    [Fact]
    public async Task Seed_IsIdempotentAndDoesNotOverwriteExistingProducts()
    {
        using var context = CreateContext();
        var seeder = new FragranceCatalogueSeeder();
        await seeder.SeedAsync(context);
        var firstProduct = await context.Products.FirstAsync();
        firstProduct.Name = "Admin-edited intake record";
        firstProduct.Price = 123m;
        firstProduct.IsPriceApproved = true;
        await context.SaveChangesAsync();

        var second = await seeder.SeedAsync(context);

        Assert.Equal(0, second.Added);
        Assert.Equal(10, second.SkippedExisting);
        Assert.Equal(10, await context.Products.CountAsync());
        Assert.Equal("Admin-edited intake record", firstProduct.Name);
        Assert.Equal(123m, firstProduct.Price);
        Assert.True(firstProduct.IsPriceApproved);
    }

    [Fact]
    public async Task Seed_SkipsMatchingExistingRecordWithAnotherSku()
    {
        using var context = CreateContext();
        var first = FragranceCatalogueSeeder.Catalogue[0];
        context.Products.Add(new Product
        {
            Sku = "MERCHANT-OWNED-1", Name = first.Name, Brand = first.Brand,
            Description = "Existing merchant product", Category = "Fragrances", Price = 333m,
            StockQuantity = 4, IsActive = true
        });
        await context.SaveChangesAsync();

        var result = await new FragranceCatalogueSeeder().SeedAsync(context);

        Assert.Equal(9, result.Added);
        Assert.Equal(1, result.SkippedExisting);
        var preserved = await context.Products.SingleAsync(p => p.Sku == "MERCHANT-OWNED-1");
        Assert.Equal(333m, preserved.Price);
        Assert.Equal("Existing merchant product", preserved.Description);
    }

    [Fact]
    public void Seeder_RejectsNonDevelopmentEnvironments()
    {
        Assert.Throws<InvalidOperationException>(() => FragranceCatalogueSeeder.EnsureDevelopmentEnvironment("Production"));
        FragranceCatalogueSeeder.EnsureDevelopmentEnvironment("Development");
    }
}
