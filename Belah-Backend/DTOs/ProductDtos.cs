using System.ComponentModel.DataAnnotations;

namespace Belah_Backend.DTOs
{
    public class ProductVariantDto
    {
        public int? Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Sku { get; set; }

        [Range(0, 1000000)]
        public decimal Price { get; set; }

        [Range(0.01, 1000000)]
        public decimal? SalePrice { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(2048)]
        public string? ImageUrl { get; set; }

    }

    public class CreateProductDto
    {
        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Product description is required.")]
        [StringLength(4000, ErrorMessage = "Product description cannot exceed 4000 characters.")]
        public string? Description { get; set; }

        [Range(0, 1000000, ErrorMessage = "Price must be between 0 and 1,000,000.")]
        public decimal Price { get; set; }

        [Range(0.01, 1000000)]
        public decimal? SalePrice { get; set; }

        [StringLength(2048, ErrorMessage = "Image URL cannot exceed 2048 characters.")]
        public string? ImageUrl { get; set; }

        [StringLength(160)]
        public string? Brand { get; set; }

        [StringLength(120)]
        public string? ProductType { get; set; }

        public int? CategoryId { get; set; }

        public bool IsFeatured { get; set; }

        public bool IsPriceApproved { get; set; } = true;

        public bool IsStockConfirmed { get; set; } = true;

        public List<ProductVariantDto> Variants { get; set; } = new();

        [Required(ErrorMessage = "Product category is required.")]
        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
        public string? Category { get; set; }

        [StringLength(4000, ErrorMessage = "Specifications cannot exceed 4000 characters.")]
        public string? Specification { get; set; }

        [StringLength(100, ErrorMessage = "SKU cannot exceed 100 characters.")]
        public string? Sku { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateStockDto
    {
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; }
    }

    public class UpdateProductStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class UpdateProductDto
    {
        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Product description is required.")]
        [StringLength(4000, ErrorMessage = "Product description cannot exceed 4000 characters.")]
        public string? Description { get; set; }

        [Range(0, 1000000, ErrorMessage = "Price must be between 0 and 1,000,000.")]
        public decimal Price { get; set; }

        [Range(0.01, 1000000)]
        public decimal? SalePrice { get; set; }

        [StringLength(2048, ErrorMessage = "Image URL cannot exceed 2048 characters.")]
        public string? ImageUrl { get; set; }

        public bool RemoveImage { get; set; }

        [StringLength(160)]
        public string? Brand { get; set; }

        [StringLength(120)]
        public string? ProductType { get; set; }

        public int? CategoryId { get; set; }

        public bool IsFeatured { get; set; }

        public bool IsPriceApproved { get; set; } = true;

        public bool IsStockConfirmed { get; set; } = true;

        public List<ProductVariantDto> Variants { get; set; } = new();

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
        public string? Category { get; set; }

        [StringLength(4000, ErrorMessage = "Specifications cannot exceed 4000 characters.")]
        public string? Specification { get; set; }

        [StringLength(100, ErrorMessage = "SKU cannot exceed 100 characters.")]
        public string? Sku { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
