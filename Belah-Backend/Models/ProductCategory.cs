using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Belah_Backend.Models;

public sealed class ProductCategory
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(110)]
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
