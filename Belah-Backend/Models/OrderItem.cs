using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Belah_Backend.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        public int? ProductVariantId { get; set; }

        public ProductVariant? ProductVariant { get; set; }

        [StringLength(100)]
        public string? SelectedVariantName { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000)]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000000)]
        public decimal Subtotal { get; set; }

        [StringLength(50)]
        public string? SelectedColor { get; set; }

        [StringLength(50)]
        public string? SelectedStorage { get; set; }
    }
}
