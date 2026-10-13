using Belah_Backend.Data;
using Belah_Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Tests;

public sealed class DevelopmentMockProductSeederTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"MockProducts_{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task Seeding_Is_Idempotent_Refreshes_Only_Marked_Mock_Rows_And_Uses_Expected_Categories()
    {
        using var context = CreateContext();
        var seeder = new DevelopmentMockProductSeeder();

        var first = await seeder.SeedAsync(context);
        Assert.Equal(14, first.Total);
        Assert.Equal(14, first.Added);
        Assert.Equal(0, first.Updated);
        Assert.Equal(14, await context.Products.CountAsync());
        Assert.Equal(14, await context.Products.Select(p => p.Sku).Distinct().CountAsync());
        Assert.Contains(await context.Products.Select(p => p.Category).Distinct().ToListAsync(), c => c == "Wigs");
        Assert.Contains(await context.Products.Select(p => p.Category).Distinct().ToListAsync(), c => c == "Bundles");
        Assert.Contains(await context.Products.Select(p => p.Category).Distinct().ToListAsync(), c => c == "Closures");
        Assert.Contains(await context.Products.Select(p => p.Category).Distinct().ToListAsync(), c => c == "Frontals");
        Assert.All(await context.Products.ToListAsync(), product =>
        {
            Assert.True(product.IsActive);
            Assert.True(product.Price > 0);
            Assert.True(product.StockQuantity > 0);
            Assert.StartsWith(DevelopmentMockProductSeeder.SampleMarker, product.Description);
            Assert.StartsWith("/images/", product.ImageUrl);
        });

        var changedSeededProduct = await context.Products.FirstAsync();
        changedSeededProduct.StockQuantity = 0;
        changedSeededProduct.Name = "Locally edited mock";
        await context.SaveChangesAsync();

        var second = await seeder.SeedAsync(context);
        Assert.Equal(0, second.Added);
        Assert.Equal(14, second.Updated);
        Assert.Equal(0, second.SkippedConflicts);
        Assert.Equal(14, await context.Products.CountAsync());
        Assert.NotEqual("Locally edited mock", (await context.Products.FindAsync(changedSeededProduct.Id))!.Name);
    }

    [Fact]
    public async Task Reserved_Sku_Collision_Without_Seed_Marker_Is_Skipped_Without_Changing_Real_Product()
    {
        using var context = CreateContext();
        var realProduct = new Belah_Backend.Models.Product
        {
            Sku = "BELAH-DEV-MOCK-001", Name = "Real listed wig", Description = "Existing merchant listing",
            Category = "Wigs", Price = 3200m, StockQuantity = 3, ImageUrl = "https://example.invalid/real.jpg", IsActive = true
        };
        context.Products.Add(realProduct);
        await context.SaveChangesAsync();

        var result = await new DevelopmentMockProductSeeder().SeedAsync(context);
        Assert.Equal(1, result.SkippedConflicts);
        Assert.Equal(13, result.Added);
        Assert.Equal("Real listed wig", realProduct.Name);
        Assert.Equal(3200m, realProduct.Price);
        Assert.Equal(14, await context.Products.CountAsync());
    }

    [Fact]
    public void Seeder_Rejects_NonDevelopment_Environments()
    {
        Assert.Throws<InvalidOperationException>(() => DevelopmentMockProductSeeder.EnsureDevelopmentEnvironment("Production"));
        DevelopmentMockProductSeeder.EnsureDevelopmentEnvironment("Development");
    }
}
