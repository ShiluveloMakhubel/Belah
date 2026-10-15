using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Belah_Backend.Models;

public sealed class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [JsonIgnore]
    public Product? Product { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Sku { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalePrice { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(2048)]
    public string? ImageUrl { get; set; }
}
