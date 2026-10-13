using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Belah_Backend.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        // Nullable for guest orders; customer account identity is stable even if email changes.
        public int? UserId { get; set; }
        public User? User { get; set; }

        // Guest bearer capability is returned once at checkout; only its SHA-256 hash is stored.
        [StringLength(64)]
        public string? GuestCapabilityHash { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? PaymentExpiresAt { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Pending";

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000000)]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "Customer name is required.")]
        [StringLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(50)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Delivery address is required.")]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Province is required.")]
        [StringLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "Postal code is required.")]
        [StringLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [StringLength(100)]
        public string? PaymentReference { get; set; }

        public long? PaystackTransactionId { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
