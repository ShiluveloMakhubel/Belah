using System.ComponentModel.DataAnnotations;

namespace Belah_Backend.DTOs;

public sealed class ProductCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class SaveProductCategoryDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
