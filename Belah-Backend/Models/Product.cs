using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Belah_Backend.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Product description is required.")]
        public string? Description { get; set; }

        [Range(0, 1000000, ErrorMessage = "Price must be between 0 and 1,000,000.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalePrice { get; set; }

        public string? ImageUrl { get; set; }

        [StringLength(160)]
        public string? Brand { get; set; }

        [StringLength(120)]
        public string? ProductType { get; set; }

        public bool IsFeatured { get; set; }

        // Defaults preserve the current purchase behaviour for existing catalogue rows.
        public bool IsPriceApproved { get; set; } = true;

        public bool IsStockConfirmed { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public int? CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        [System.Text.Json.Serialization.JsonIgnore]
        public ProductCategory? ProductCategory { get; set; }

        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
        public string? Category { get; set; }

        public string? Specification { get; set; }

        [StringLength(100, ErrorMessage = "SKU cannot exceed 100 characters.")]
        public string? Sku { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
