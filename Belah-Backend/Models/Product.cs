using System.ComponentModel.DataAnnotations;

namespace Belah_Backend.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; } // Made nullable
        [Required]
        public string? Description { get; set; } // Made nullable
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; } // Made nullable
        public string? Category { get; set; } // Made nullable
        public string? Specification { get; set; } // Made nullable
    }
}
