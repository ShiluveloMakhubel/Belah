using System.Globalization;
using System.Text;
using Belah_Backend.Data;
using Belah_Backend.DTOs;
using Belah_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Belah_Backend.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class ProductCategoriesController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductCategoryDto>>> Get([FromQuery] bool includeInactive = false)
    {
        var query = context.ProductCategories.AsNoTracking();
        if (!includeInactive || !User.IsInRole("Admin")) query = query.Where(c => c.IsActive);
        return Ok(await query.OrderBy(c => c.Name)
            .Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name, Slug = c.Slug, IsActive = c.IsActive })
            .ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductCategoryDto>> GetById(int id, [FromQuery] bool includeInactive = false)
    {
        var category = await context.ProductCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category == null || (!category.IsActive && !(includeInactive && User.IsInRole("Admin")))) return NotFound();
        return Ok(ToDto(category));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductCategoryDto>> Create(SaveProductCategoryDto dto)
    {
        var name = dto.Name.Trim();
        if (await context.ProductCategories.AnyAsync(c => c.Name == name))
            return Conflict(new { message = "A category with this name already exists." });
        var category = new ProductCategory { Name = name, Slug = Slugify(name), IsActive = dto.IsActive };
        if (await context.ProductCategories.AnyAsync(c => c.Slug == category.Slug))
            return Conflict(new { message = "A category with a similar name already exists." });
        context.ProductCategories.Add(category);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ToDto(category));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductCategoryDto>> Update(int id, SaveProductCategoryDto dto)
    {
        var category = await context.ProductCategories.FindAsync(id);
        if (category == null) return NotFound();
        var name = dto.Name.Trim();
        if (await context.ProductCategories.AnyAsync(c => c.Id != id && (c.Name == name || c.Slug == Slugify(name))))
            return Conflict(new { message = "A category with this name already exists." });
        category.Name = name;
        category.Slug = Slugify(name);
        category.IsActive = dto.IsActive;
        var linkedProducts = await context.Products.Where(p => p.CategoryId == id).ToListAsync();
        foreach (var product in linkedProducts)
        {
            product.Category = name;
            product.UpdatedAtUtc = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
        return Ok(ToDto(category));
    }

    private static ProductCategoryDto ToDto(ProductCategory category) => new()
    {
        Id = category.Id, Name = category.Name, Slug = category.Slug, IsActive = category.IsActive
    };

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }
        return string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
