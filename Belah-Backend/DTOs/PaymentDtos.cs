using System.ComponentModel.DataAnnotations;

namespace Belah_Backend.DTOs
{
    public class InitializePaymentRequest
    {
        [Required(ErrorMessage = "Order number is required.")]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        [StringLength(128)]
        public string? GuestCapability { get; set; }
    }

    public class InitializePaymentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string PaymentReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public long AmountInMinorUnits { get; set; }
        public string Currency { get; set; } = "ZAR";
        public string CustomerEmail { get; set; } = string.Empty;
        public string? AuthorizationUrl { get; set; }
        public string? AccessCode { get; set; }
    }

    public class VerifyPaymentRequest
    {
        [Required(ErrorMessage = "Transaction reference is required.")]
        [StringLength(100)]
        public string Reference { get; set; } = string.Empty;

        [StringLength(128)]
        public string? GuestCapability { get; set; }
    }

    public class VerifyPaymentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
    }
}
