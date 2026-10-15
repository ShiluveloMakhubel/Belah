using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Belah_Backend.DTOs
{
    public class CreateOrderRequest
    {
        [Required(ErrorMessage = "Customer name is required.")]
        [StringLength(200, ErrorMessage = "Customer name cannot exceed 200 characters.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Delivery address is required.")]
        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Province is required.")]
        [StringLength(100, ErrorMessage = "Province cannot exceed 100 characters.")]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "Postal code is required.")]
        [StringLength(20, ErrorMessage = "Postal code cannot exceed 20 characters.")]
        public string PostalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Order must contain at least one item.")]
        [MinLength(1, ErrorMessage = "Order must contain at least one item.")]
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }

    public class CreateOrderItemDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid Product ID is required.")]
        public int ProductId { get; set; }

        public int? VariantId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [StringLength(50)]
        public string? SelectedColor { get; set; }

        [StringLength(50)]
        public string? SelectedStorage { get; set; }
    }

    public class OrderResponseDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal SubtotalAmount { get; set; }
        public string? PaymentReference { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public List<OrderItemResponseDto> Items { get; set; } = new();
    }

    public class CreateOrderResponseDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal SubtotalAmount { get; set; }
        public string? GuestCapability { get; set; }
    }

    public class OrderItemResponseDto
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SelectedVariantName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? SelectedColor { get; set; }
        public string? SelectedStorage { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        [Required(ErrorMessage = "Status is required.")]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        [StringLength(50)]
        public string? PaymentStatus { get; set; }
    }

    /// <summary>
    /// Privacy-safe public order tracking DTO.
    /// Does NOT expose customer email, phone, physical address, or payment references to anonymous callers.
    /// </summary>
    public class PublicOrderTrackingDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public List<PublicOrderItemTrackingDto> Items { get; set; } = new();
    }

    public class PublicOrderItemTrackingDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string? SelectedVariantName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? SelectedColor { get; set; }
        public string? SelectedStorage { get; set; }
    }
}
