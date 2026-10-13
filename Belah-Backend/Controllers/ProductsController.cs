using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Belah_Backend.Data;
using Belah_Backend.Models;
using Belah_Backend.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Belah_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Image upload constraints
        private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // 1. GET: api/products
        // Return active products by default. Support includeInactive only if caller is Admin.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] bool includeInactive = false)
        {
            // Anonymous and non-admin users cannot request inactive products.
            bool allowInactive = includeInactive && User.IsInRole("Admin");

            IQueryable<Product> query = _context.Products.AsNoTracking();

            if (!allowInactive)
            {
                query = query.Where(p => p.IsActive);
            }

            var products = await query.ToListAsync();
            return Ok(products);
        }

        // 2. GET: api/products/{id}
        // Return active product. Inactive products return NotFound unless caller is Admin.
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id, [FromQuery] bool includeInactive = false)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            bool allowInactive = includeInactive && User.IsInRole("Admin");

            if (!product.IsActive && !allowInactive)
            {
                return NotFound(new { message = "Product is inactive and unavailable." });
            }

            return Ok(product);
        }

        // 3. POST: api/products
        // Admin only: create product
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Product data cannot be null.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var validationError = ValidateProduct(dto.Name, dto.Description, dto.Category, dto.Price, dto.StockQuantity, dto.ImageUrl, dto.Specification);
            if (validationError != null) return BadRequest(new { message = validationError });
            if (string.IsNullOrWhiteSpace(dto.ImageUrl)) return BadRequest(new { message = "A product image URL or uploaded image is required." });

            if (dto.StockQuantity < 0)
            {
                return BadRequest(new { message = "Stock quantity cannot be negative." });
            }

            if (dto.Price < 0)
            {
                return BadRequest(new { message = "Price cannot be negative." });
            }

            // Product defaults
            dto.Sku = string.IsNullOrWhiteSpace(dto.Sku) ? null : dto.Sku.Trim();

            if (dto.Sku != null && await _context.Products.AnyAsync(p => p.Sku == dto.Sku))
                return Conflict(new { message = "A product with this SKU already exists." });

            var product = new Product
            {
                Name = dto.Name!.Trim(), Description = dto.Description!.Trim(), Price = dto.Price,
                Category = dto.Category!.Trim(), ImageUrl = dto.ImageUrl!.Trim(), Specification = dto.Specification,
                Sku = dto.Sku, StockQuantity = dto.StockQuantity, IsActive = dto.IsActive
            };

            await _context.Products.AddAsync(product);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsSkuUniqueConstraintViolation(ex))
            {
                return Conflict(new { message = "A product with this SKU already exists." });
            }

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        // 4. PUT: api/products/{id}
        // Update product information, validate all input, prevent negative stock. Admin only.
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var validationError = ValidateProduct(dto.Name, dto.Description, dto.Category, dto.Price, dto.StockQuantity, dto.ImageUrl, dto.Specification);
            if (validationError != null) return BadRequest(new { message = validationError });

            dto.Sku = string.IsNullOrWhiteSpace(dto.Sku) ? null : dto.Sku.Trim();
            if (dto.Sku != null && await _context.Products.AnyAsync(p => p.Id != id && p.Sku == dto.Sku))
                return Conflict(new { message = "A product with this SKU already exists." });

            if (dto.StockQuantity < 0)
            {
                return BadRequest(new { message = "Stock quantity cannot be negative." });
            }

            if (dto.Price < 0)
            {
                return BadRequest(new { message = "Price cannot be negative." });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl)) product.ImageUrl = dto.ImageUrl;
            product.Category = dto.Category;
            product.Specification = dto.Specification;
            product.Sku = dto.Sku;
            product.StockQuantity = dto.StockQuantity;
            product.IsActive = dto.IsActive;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsSkuUniqueConstraintViolation(ex))
            {
                return Conflict(new { message = "A product with this SKU already exists." });
            }

            return Ok(product);
        }

        // 5. PATCH: api/products/{id}/stock
        // Update stock quantity and reject negative quantities. Admin only.
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/stock")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto request)
        {
            if (request == null || request.StockQuantity < 0)
            {
                return BadRequest(new { message = "Stock quantity cannot be negative." });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            product.StockQuantity = request.StockQuantity;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Stock updated successfully.",
                productId = product.Id,
                stockQuantity = product.StockQuantity
            });
        }

        // 6. PATCH: api/products/{id}/status
        // Activate/deactivate a product. Admin only.
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateProductStatusDto request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Status payload is required." });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            product.IsActive = request.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Product {(product.IsActive ? "activated" : "deactivated")} successfully.",
                productId = product.Id,
                isActive = product.IsActive
            });
        }

        // 7. POST: api/products/upload
        // Prevent directory traversal, whitelist content types/extensions, limit size, generate safe filename. Admin only.
        [Authorize(Roles = "Admin")]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            if (file.Length > MaxFileSizeInBytes)
            {
                return BadRequest(new { message = "File size exceeds the 5 MB limit." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"Invalid file extension '{extension}'. Allowed: {string.Join(", ", AllowedExtensions)}" });
            }

            if (!string.IsNullOrEmpty(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            {
                return BadRequest(new { message = $"Invalid content type '{file.ContentType}'. Allowed: {string.Join(", ", AllowedContentTypes)}" });
            }

            if (!await HasValidImageSignature(file, extension))
                return BadRequest(new { message = "The uploaded file content is not a valid image of the selected type." });

            // Define the folder where files are stored inside wwwroot
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var folderPath = Path.Combine(webRoot, "uploads");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Generate safe unique filename to avoid collision and path traversal
            var safeFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(folderPath, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/{safeFileName}";

            return Ok(new { url = fileUrl });
        }

        private static string? ValidateProduct(string? name, string? description, string? category, decimal price, int stock, string? imageUrl, string? specification)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Product name is required.";
            if (string.IsNullOrWhiteSpace(description)) return "Product description is required.";
            if (description.Length > 4000) return "Product description cannot exceed 4000 characters.";
            if (string.IsNullOrWhiteSpace(category)) return "Product category is required.";
            if (price < 0 || price > 1000000m || decimal.Round(price, 2) != price) return "Price must be between R0.00 and R1,000,000.00 with no more than two decimal places.";
            if (stock < 0) return "Stock quantity cannot be negative.";
            if (imageUrl?.Length > 2048) return "Image URL cannot exceed 2048 characters.";
            if (specification?.Length > 4000) return "Specifications cannot exceed 4000 characters.";
            if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl.StartsWith('/') && imageUrl.StartsWith("//", StringComparison.Ordinal)) return "Image URL must be a site-relative path.";
            if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.StartsWith('/') &&
                (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute) || absolute.Scheme is not ("http" or "https")))
                return "Image URL must use HTTP or HTTPS or a site-relative path.";
            return null;
        }

        private static bool IsSkuUniqueConstraintViolation(DbUpdateException exception)
        {
            var message = exception.InnerException?.Message;
            return message != null && (
                message.Contains("IX_Products_Sku", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("UNIQUE constraint failed: Products.Sku", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<bool> HasValidImageSignature(IFormFile file, string extension)
        {
            var header = new byte[Math.Min(12, (int)file.Length)];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header);
            if (extension is ".jpg" or ".jpeg") return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            if (extension == ".png") return read >= 8 && header.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            if (extension == ".gif") return read >= 6 && System.Text.Encoding.ASCII.GetString(header, 0, 6) is "GIF87a" or "GIF89a";
            if (extension == ".webp") return read >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
            return false;
        }
    }
}
