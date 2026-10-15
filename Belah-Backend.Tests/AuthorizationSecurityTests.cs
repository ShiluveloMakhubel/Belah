using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Belah_Backend.Controllers;
using Belah_Backend.Data;
using Belah_Backend.DTOs;
using Belah_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Belah_Backend.Tests
{
    public class AuthorizationSecurityTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new ApplicationDbContext(options);
        }

        private static ControllerContext CreateControllerContext(string? role = null)
        {
            var claims = new List<Claim>();
            if (!string.IsNullOrEmpty(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim(ClaimTypes.Name, $"TestUser_{role}"));
            }

            var identity = string.IsNullOrEmpty(role)
                ? new ClaimsIdentity()
                : new ClaimsIdentity(claims, "TestAuthType");

            var user = new ClaimsPrincipal(identity);
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        // ==========================================
        // 1. PRODUCTS AUTHORIZATION ATTRIBUTE CHECKS
        // ==========================================

        [Fact]
        public void Product_Creation_Requires_Admin_Role()
        {
            var method = typeof(ProductsController).GetMethod(nameof(ProductsController.CreateProduct));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        [Fact]
        public void Product_Update_Requires_Admin_Role()
        {
            var method = typeof(ProductsController).GetMethod(nameof(ProductsController.UpdateProduct));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        [Fact]
        public void Product_StockUpdate_Requires_Admin_Role()
        {
            var method = typeof(ProductsController).GetMethod(nameof(ProductsController.UpdateStock));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        [Fact]
        public void Product_StatusUpdate_Requires_Admin_Role()
        {
            var method = typeof(ProductsController).GetMethod(nameof(ProductsController.UpdateStatus));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        [Fact]
        public void Product_ImageUpload_Requires_Admin_Role()
        {
            var method = typeof(ProductsController).GetMethod(nameof(ProductsController.Upload));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        // ========================================================
        // 2. PRODUCT BROWSING & INACTIVE PRODUCT VISIBILITY TESTS
        // ========================================================

        [Fact]
        public async Task Anonymous_User_Cannot_Access_Inactive_Products_Via_GetProducts()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.AddRange(
                new Product { Id = 1, Name = "Active Wig", Description = "Premium wig", Price = 1000m, IsActive = true, StockQuantity = 5 },
                new Product { Id = 2, Name = "Hidden Inactive Wig", Description = "Hidden wig", Price = 1200m, IsActive = false, StockQuantity = 5 }
            );
            await context.SaveChangesAsync();

            var controller = new ProductsController(context, null!)
            {
                ControllerContext = CreateControllerContext(role: null) // Anonymous
            };

            var result = await controller.GetProducts(includeInactive: true);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value).ToList();

            Assert.Single(products);
            Assert.Equal("Active Wig", products[0].Name);
        }

        [Fact]
        public async Task Customer_Cannot_Access_Inactive_Products_Via_GetProducts()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.AddRange(
                new Product { Id = 1, Name = "Active Wig", Description = "Premium wig", Price = 1000m, IsActive = true, StockQuantity = 5 },
                new Product { Id = 2, Name = "Hidden Inactive Wig", Description = "Hidden wig", Price = 1200m, IsActive = false, StockQuantity = 5 }
            );
            await context.SaveChangesAsync();

            var controller = new ProductsController(context, null!)
            {
                ControllerContext = CreateControllerContext(role: "Customer")
            };

            var result = await controller.GetProducts(includeInactive: true);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value).ToList();

            Assert.Single(products);
            Assert.Equal("Active Wig", products[0].Name);
        }

        [Fact]
        public async Task Admin_Can_Access_Inactive_Products_When_Requested()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.AddRange(
                new Product { Id = 1, Name = "Active Wig", Description = "Premium wig", Price = 1000m, IsActive = true, StockQuantity = 5 },
                new Product { Id = 2, Name = "Hidden Inactive Wig", Description = "Hidden wig", Price = 1200m, IsActive = false, StockQuantity = 5 }
            );
            await context.SaveChangesAsync();

            var controller = new ProductsController(context, null!)
            {
                ControllerContext = CreateControllerContext(role: "Admin")
            };

            var result = await controller.GetProducts(includeInactive: true);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value).ToList();

            Assert.Equal(2, products.Count);
        }

        [Fact]
        public async Task Anonymous_User_Gets_NotFound_For_Inactive_Product_Detail()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.Add(new Product { Id = 1, Name = "Inactive Wig", Description = "Hidden wig", Price = 1000m, IsActive = false, StockQuantity = 5 });
            await context.SaveChangesAsync();

            var controller = new ProductsController(context, null!)
            {
                ControllerContext = CreateControllerContext(role: null) // Anonymous
            };

            var result = await controller.GetProduct(1, includeInactive: true);
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task Admin_Can_View_Inactive_Product_Detail()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.Add(new Product { Id = 1, Name = "Inactive Wig", Description = "Hidden wig", Price = 1000m, IsActive = false, StockQuantity = 5 });
            await context.SaveChangesAsync();

            var controller = new ProductsController(context, null!)
            {
                ControllerContext = CreateControllerContext(role: "Admin")
            };

            var result = await controller.GetProduct(1, includeInactive: true);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var product = Assert.IsType<Product>(okResult.Value);
            Assert.Equal("Inactive Wig", product.Name);
        }

        private static ProductsController ProductController(ApplicationDbContext context, string? role = "Admin") => new(context, null!)
        {
            ControllerContext = CreateControllerContext(role)
        };

        [Fact]
        public async Task Admin_Creates_Product_And_Persisted_Id_And_Public_Fields_Are_Returned()
        {
            Assert.Null(typeof(CreateProductDto).GetProperty("Id"));
            using var context = CreateInMemoryDbContext();
            var product = new CreateProductDto { Name = "Body Wave Wig", Description = "Natural hair wig", Category = "Wigs", Price = 2499.99m, StockQuantity = 4, Sku = "WIG-BW-01", Specification = "18 inch; 180% density", IsActive = true, ImageUrl = "/uploads/wig.png" };
            var result = await ProductController(context).CreateProduct(product);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returned = Assert.IsType<Product>(created.Value);
            Assert.True(returned.Id > 0);
            var stored = await context.Products.AsNoTracking().SingleAsync();
            Assert.Equal(returned.Id, stored.Id);
            Assert.Equal("Wigs", stored.Category);
            Assert.Equal(2499.99m, stored.Price);
            Assert.Equal(4, stored.StockQuantity);
            Assert.True(stored.IsActive);
            Assert.Equal("/uploads/wig.png", stored.ImageUrl);
        }

        [Theory]
        [InlineData(-1, 2)]
        [InlineData(10, -1)]
        public async Task Product_Create_Rejects_Negative_Price_Or_Stock(decimal price, int stock)
        {
            using var context = CreateInMemoryDbContext();
            var product = new CreateProductDto { Name = "Wig", Description = "Description", Category = "Wigs", Price = price, StockQuantity = stock, ImageUrl = "/uploads/wig.png" };
            var result = await ProductController(context).CreateProduct(product);
            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(context.Products);
        }

        [Fact]
        public async Task Product_Create_Rejects_Malformed_And_Missing_Required_Fields()
        {
            using var context = CreateInMemoryDbContext();
            var product = new CreateProductDto { Name = " ", Description = "Description", Category = "Wigs", Price = 10.123m, StockQuantity = 0, ImageUrl = "javascript:alert(1)" };
            var result = await ProductController(context).CreateProduct(product);
            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(context.Products);
        }

        [Fact]
        public async Task Product_Update_Persists_InPlace_Without_Creating_Duplicate()
        {
            using var context = CreateInMemoryDbContext();
            var product = new Product { Name = "Old name", Description = "Old description", Category = "Wigs", Price = 100, StockQuantity = 2 };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var id = product.Id;
            var dto = new UpdateProductDto { Name = "New name", Description = "Updated details", Category = "Frontals", Price = 200.50m, StockQuantity = 8, IsActive = true, ImageUrl = "/uploads/front.png", Specification = "13x4", Sku = "FRONT-1" };
            var result = await ProductController(context).UpdateProduct(id, dto);
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(1, await context.Products.CountAsync());
            var saved = await context.Products.SingleAsync();
            Assert.Equal(id, saved.Id);
            Assert.Equal("New name", saved.Name);
            Assert.Equal(200.50m, saved.Price);
            Assert.Equal("Frontals", saved.Category);
            Assert.Equal(8, saved.StockQuantity);
            Assert.Equal("/uploads/front.png", saved.ImageUrl);
        }

        [Fact]
        public async Task Product_Create_Persists_Brand_And_Variant_Prices_Stock_And_Image_And_SKUs_Are_Unique()
        {
            using var context = CreateInMemoryDbContext();
            var productDto = new CreateProductDto
            {
                Name = "Fragrance", Description = "Verified product details", Category = "Fragrances",
                Brand = "Example House", ProductType = "Eau de Parfum", Price = 0, StockQuantity = 0,
                ImageUrl = "/images/product-placeholder.svg",
                Variants = new List<ProductVariantDto>
                {
                    new() { Name = "50 ml", Sku = "FRAG-50", Price = 900, StockQuantity = 2 },
                    new() { Name = "100 ml", Sku = "FRAG-100", Price = 1400, StockQuantity = 3 }
                }
            };
            var result = await ProductController(context).CreateProduct(productDto);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var saved = Assert.IsType<Product>(created.Value);
            Assert.Equal("Example House", saved.Brand);
            Assert.Equal(900m, saved.Price);
            Assert.Equal(5, saved.StockQuantity);
            Assert.Equal(2, await context.ProductVariants.CountAsync());

            var duplicateSku = await ProductController(context).CreateProduct(new CreateProductDto
            {
                Name = "Other fragrance", Description = "Details", Category = "Fragrances", Price = 2,
                ImageUrl = "/images/product-placeholder.svg", Variants = new List<ProductVariantDto>
                {
                    new() { Name = "50 ml", Sku = "FRAG-50", Price = 2, StockQuantity = 1 }
                }
            });
            Assert.IsType<ConflictObjectResult>(duplicateSku.Result);
            Assert.Equal(1, await context.Products.CountAsync());
        }

        [Fact]
        public async Task Admin_Can_Remove_Product_Image_Reference_Without_Deleting_Product()
        {
            using var context = CreateInMemoryDbContext();
            var product = new Product { Name = "Fragrance", Description = "Details", Category = "Fragrances", Price = 1, StockQuantity = 1, ImageUrl = "/uploads/owned.png" };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var result = await ProductController(context).UpdateProduct(product.Id, new UpdateProductDto
            {
                Name = product.Name, Description = product.Description, Category = product.Category,
                Price = product.Price, StockQuantity = product.StockQuantity, RemoveImage = true
            });
            Assert.IsType<OkObjectResult>(result);
            Assert.Null((await context.Products.SingleAsync()).ImageUrl);
        }

        [Fact]
        public async Task Admin_Manages_Categories_And_Renaming_Updates_Assigned_Products()
        {
            using var context = CreateInMemoryDbContext();
            var admin = new ProductCategoriesController(context) { ControllerContext = CreateControllerContext("Admin") };
            var created = await admin.Create(new SaveProductCategoryDto { Name = "Fragrances", IsActive = true });
            var categoryResult = Assert.IsType<CreatedAtActionResult>(created.Result);
            var category = Assert.IsType<ProductCategoryDto>(categoryResult.Value);
            var product = new Product { Name = "Test scent", Description = "Details", Category = category.Name, CategoryId = category.Id, Price = 10 };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var updated = await admin.Update(category.Id, new SaveProductCategoryDto { Name = "Scents", IsActive = true });
            Assert.IsType<OkObjectResult>(updated.Result);
            Assert.Equal("Scents", (await context.Products.SingleAsync()).Category);
            Assert.Equal("scents", (await context.ProductCategories.SingleAsync()).Slug);
        }

        [Fact]
        public async Task Duplicate_SKU_Is_Rejected_On_Create_And_Update()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.AddRange(
                new Product { Name = "Existing", Description = "Description", Category = "Wigs", Price = 1, Sku = "SKU-1" },
                new Product { Name = "Other", Description = "Description", Category = "Wigs", Price = 1, Sku = "SKU-2" });
            await context.SaveChangesAsync();
            var create = await ProductController(context).CreateProduct(new CreateProductDto { Name = "Another", Description = "Description", Category = "Wigs", Price = 2, Sku = "SKU-1", ImageUrl = "/uploads/another.png" });
            Assert.IsType<ConflictObjectResult>(create.Result);
            var existing = await context.Products.SingleAsync(p => p.Sku == "SKU-2");
            var update = await ProductController(context).UpdateProduct(existing.Id, new UpdateProductDto { Name = "Update", Description = "Description", Category = "Wigs", Price = 2, Sku = "SKU-1" });
            Assert.IsType<ConflictObjectResult>(update);
        }

        [Fact]
        public async Task Public_Catalogue_And_Detail_Only_Return_Active_Saved_Products()
        {
            using var context = CreateInMemoryDbContext();
            context.Products.AddRange(
                new Product { Name = "Visible", Description = "Public", Category = "Wigs", Price = 500, StockQuantity = 2, IsActive = true },
                new Product { Name = "Hidden", Description = "Private", Category = "Wigs", Price = 600, StockQuantity = 2, IsActive = false });
            await context.SaveChangesAsync();
            var publicController = ProductController(context, role: null);
            var list = Assert.IsType<OkObjectResult>((await publicController.GetProducts()).Result);
            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(list.Value).ToArray();
            Assert.Single(products);
            Assert.Equal("Visible", products[0].Name);
            Assert.IsType<NotFoundObjectResult>((await publicController.GetProduct(products[0].Id + 1)).Result);
            var detail = Assert.IsType<OkObjectResult>((await publicController.GetProduct(products[0].Id)).Result);
            Assert.Equal(products[0].Id, Assert.IsType<Product>(detail.Value).Id);
        }

        [Fact]
        public async Task Product_Upload_Rejects_Fake_Image_Content()
        {
            using var context = CreateInMemoryDbContext();
            var controller = ProductController(context);
            var bytes = System.Text.Encoding.UTF8.GetBytes("this is not a jpeg");
            await using var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "image.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
            var result = await controller.Upload(file);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Order_Creation_Rejects_Inactive_And_Insufficient_Stock_Products()
        {
            using var context = CreateInMemoryDbContext();
            var hidden = new Product { Name = "Hidden wig", Description = "Details", Category = "Wigs", Price = 500, StockQuantity = 5, IsActive = false };
            var lowStock = new Product { Name = "Low stock wig", Description = "Details", Category = "Wigs", Price = 500, StockQuantity = 1, IsActive = true };
            var unconfirmedStock = new Product { Name = "Unconfirmed wig", Description = "Details", Category = "Wigs", Price = 500, StockQuantity = 5, IsActive = true, IsStockConfirmed = false };
            var unapprovedPrice = new Product { Name = "Unpriced wig", Description = "Details", Category = "Wigs", Price = 500, StockQuantity = 5, IsActive = true, IsPriceApproved = false };
            context.Products.AddRange(hidden, lowStock, unconfirmedStock, unapprovedPrice);
            await context.SaveChangesAsync();
            var controller = new OrdersController(context) { ControllerContext = CreateControllerContext() };
            var request = new CreateOrderRequest { CustomerName = "Buyer", Email = "buyer@example.com", Phone = "123456789", Address = "1 Main", City = "Cape Town", Province = "Western Cape", PostalCode = "8001" };
            request.Items.Add(new CreateOrderItemDto { ProductId = hidden.Id, Quantity = 1 });
            Assert.IsType<BadRequestObjectResult>((await controller.CreateOrder(request)).Result);
            request.Items = new List<CreateOrderItemDto> { new() { ProductId = lowStock.Id, Quantity = 2 } };
            Assert.IsType<ConflictObjectResult>((await controller.CreateOrder(request)).Result);
            request.Items = new List<CreateOrderItemDto> { new() { ProductId = unconfirmedStock.Id, Quantity = 1 } };
            Assert.IsType<ConflictObjectResult>((await controller.CreateOrder(request)).Result);
            request.Items = new List<CreateOrderItemDto> { new() { ProductId = unapprovedPrice.Id, Quantity = 1 } };
            Assert.IsType<ConflictObjectResult>((await controller.CreateOrder(request)).Result);
            Assert.Empty(context.Orders);
            Assert.Equal(1, lowStock.StockQuantity);
        }

        // ==========================================
        // 3. USER REGISTRATION ROLE TAMPERING TESTS
        // ==========================================

        [Fact]
        public void User_Registration_Always_Assigns_Customer_Role()
        {
            using var context = CreateInMemoryDbContext();
            var inMemorySettings = new Dictionary<string, string?>
            {
                {"Jwt:Key", "SuperSecretTestingSigningKeyMustBe32BytesOrLonger!"}
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var controller = new UserController(context, configuration);

            var request = new RegisterRequest
            {
                Username = "newcustomer",
                Email = "customer@example.com",
                Password = "Password123!"
            };

            var response = controller.Register(request);
            Assert.IsType<OkObjectResult>(response);

            var createdUser = context.Users.FirstOrDefault(u => u.Username == "newcustomer");
            Assert.NotNull(createdUser);
            Assert.Equal("Customer", createdUser.Role);
        }

        [Theory]
        [InlineData("Login")]
        [InlineData("Register")]
        public void Authentication_Endpoints_Use_Auth_RateLimit(string actionName)
        {
            var method = typeof(UserController).GetMethod(actionName);
            Assert.NotNull(method);
            Assert.Equal("auth", method!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }

        [Fact]
        public void Duplicate_Registration_Does_Not_Disclose_Account_Existence_Or_UserId()
        {
            using var context = CreateInMemoryDbContext();
            context.Users.Add(new User { Username = "existing", Email = "existing@example.com", PasswordHash = "test", Role = "Customer" });
            context.SaveChanges();
            var controller = new UserController(context, new ConfigurationBuilder().Build());
            var result = Assert.IsType<OkObjectResult>(controller.Register(new RegisterRequest
            {
                Username = "existing", Email = "existing@example.com", Password = "Password123!"
            }));

            var properties = result.Value!.GetType().GetProperties().Select(property => property.Name).ToArray();
            Assert.Contains("message", properties);
            Assert.DoesNotContain("userId", properties, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(1, context.Users.Count());
        }

        // ==========================================
        // 4. ORDERS AUTHORIZATION ATTRIBUTES & PRIVACY
        // ==========================================

        [Fact]
        public void GetOrders_Requires_Admin_Role()
        {
            var method = typeof(OrdersController).GetMethod(nameof(OrdersController.GetOrders));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        [Fact]
        public void UpdateOrderStatus_Requires_Admin_Role()
        {
            var method = typeof(OrdersController).GetMethod(nameof(OrdersController.UpdateOrderStatus));
            Assert.NotNull(method);
            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Admin", authAttr.Roles);
        }

        [Fact]
        public void Customer_OrderStatusEndpoint_IsProtectedByAdminRole()
        {
            var attribute = typeof(OrdersController).GetMethod(nameof(OrdersController.UpdateOrderStatus))!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal("Admin", attribute!.Roles);
        }

        [Theory]
        [InlineData("Pending", "Processing", "Pending", false)]
        [InlineData("Pending", "Processing", "Paid", true)]
        [InlineData("Processing", "Shipped", "Paid", true)]
        [InlineData("Shipped", "Completed", "Paid", true)]
        [InlineData("Completed", "Processing", "Paid", false)]
        [InlineData("RefundRequired", "Processing", "RefundRequired", false)]
        [InlineData("Pending", "RefundRequired", "Paid", false)]
        [InlineData("Pending", "Paid", "Paid", false)]
        public async Task Admin_FulfilmentStatus_EnforcesPaymentAndTransitionRules(string current, string requested, string payment, bool succeeds)
        {
            using var context = CreateInMemoryDbContext();
            var order = new Order { OrderNumber = $"BELAH-{current}-{requested}-{payment}", CustomerName = "Buyer", Email = "buyer@example.com", Phone = "123", Address = "Street", City = "City", Province = "Province", PostalCode = "0000", TotalAmount = 100m, Status = current, PaymentStatus = payment };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var controller = new OrdersController(context) { ControllerContext = CreateControllerContext("Admin") };

            var result = await controller.UpdateOrderStatus(order.Id, new UpdateOrderStatusDto { Status = requested });

            if (succeeds)
            {
                var response = Assert.IsType<OkObjectResult>(result);
                Assert.Equal(requested, Assert.IsType<OrderResponseDto>(response.Value).Status);
                Assert.Equal(requested, (await context.Orders.FindAsync(order.Id))!.Status);
            }
            else
            {
                if (requested == "Paid") Assert.IsType<BadRequestObjectResult>(result);
                else Assert.IsType<ConflictObjectResult>(result);
                Assert.Equal(current, (await context.Orders.FindAsync(order.Id))!.Status);
            }
        }

        [Fact]
        public async Task Public_Order_Tracking_Does_Not_Expose_Customer_PII()
        {
            using var context = CreateInMemoryDbContext();
            var order = new Order
            {
                Id = 1,
                OrderNumber = "BELAH-20261010-ABCD12",
                CustomerName = "John Doe",
                Email = "john.doe@example.com",
                Phone = "+27123456789",
                Address = "123 Private St",
                City = "Cape Town",
                Province = "Western Cape",
                PostalCode = "8001",
                PaymentReference = "paystack_secret_ref_999",
                TotalAmount = 2500m,
                Status = "Pending",
                PaymentStatus = "Pending",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = 1,
                        ProductId = 10,
                        ProductName = "Virgin Lace Wig",
                        Quantity = 1,
                        UnitPrice = 2500m,
                        Subtotal = 2500m
                    }
                }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var controller = new OrdersController(context)
            {
                ControllerContext = CreateControllerContext(role: null) // Anonymous
            };

            var result = await controller.GetOrderByNumber("BELAH-20261010-ABCD12");
            var okResult = Assert.IsType<OkObjectResult>(result);
            var publicDto = Assert.IsType<PublicOrderTrackingDto>(okResult.Value);

            Assert.Equal("BELAH-20261010-ABCD12", publicDto.OrderNumber);
            Assert.Equal(2500m, publicDto.TotalAmount);
            Assert.Equal("Pending", publicDto.Status);

            // Confirm type contains no PII properties
            var dtoProperties = typeof(PublicOrderTrackingDto).GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("Email", dtoProperties);
            Assert.DoesNotContain("Phone", dtoProperties);
            Assert.DoesNotContain("Address", dtoProperties);
            Assert.DoesNotContain("PaymentReference", dtoProperties);
            Assert.DoesNotContain("CustomerName", dtoProperties);
        }

        [Fact]
        public async Task Admin_Order_Tracking_Returns_Full_Customer_Details()
        {
            using var context = CreateInMemoryDbContext();
            var order = new Order
            {
                Id = 1,
                OrderNumber = "BELAH-20261010-ABCD12",
                CustomerName = "John Doe",
                Email = "john.doe@example.com",
                Phone = "+27123456789",
                Address = "123 Private St",
                City = "Cape Town",
                Province = "Western Cape",
                PostalCode = "8001",
                PaymentReference = "paystack_secret_ref_999",
                TotalAmount = 2500m,
                Status = "Pending",
                PaymentStatus = "Pending",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = 1,
                        ProductId = 10,
                        ProductName = "Virgin Lace Wig",
                        Quantity = 1,
                        UnitPrice = 2500m,
                        Subtotal = 2500m
                    }
                }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var controller = new OrdersController(context)
            {
                ControllerContext = CreateControllerContext(role: "Admin")
            };

            var result = await controller.GetOrderByNumber("BELAH-20261010-ABCD12");
            var okResult = Assert.IsType<OkObjectResult>(result);
            var adminDto = Assert.IsType<OrderResponseDto>(okResult.Value);

            Assert.Equal("John Doe", adminDto.CustomerName);
            Assert.Equal("john.doe@example.com", adminDto.Email);
            Assert.Equal("paystack_secret_ref_999", adminDto.PaymentReference);
        }

        // ==========================================
        // 5. UNAUTHORIZED PAYMENT STATUS CHANGE TESTS
        // ==========================================

        [Fact]
        public async Task UpdateOrderStatus_Rejects_Setting_PaymentStatus_To_Paid()
        {
            using var context = CreateInMemoryDbContext();
            var order = new Order
            {
                Id = 1,
                OrderNumber = "BELAH-20261010-ABCD12",
                CustomerName = "John Doe",
                Email = "john.doe@example.com",
                Phone = "+27123456789",
                Address = "123 Private St",
                City = "Cape Town",
                Province = "Western Cape",
                PostalCode = "8001",
                TotalAmount = 2500m,
                Status = "Pending",
                PaymentStatus = "Pending"
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var controller = new OrdersController(context)
            {
                ControllerContext = CreateControllerContext(role: "Admin")
            };

            var updateDto = new UpdateOrderStatusDto
            {
                Status = "Processing",
                PaymentStatus = "Paid" // Unverified attempt to mark as Paid
            };

            var result = await controller.UpdateOrderStatus(1, updateDto);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);

            // Verify order in database was not marked Paid
            var persistedOrder = await context.Orders.FindAsync(1);
            Assert.Equal("Pending", persistedOrder!.PaymentStatus);
        }

        // ==========================================
        // 6. CUSTOMER ISOLATION & MY-ORDERS TESTS
        // ==========================================

        [Fact]
        public async Task Customer_Can_Access_Full_Details_Of_Their_Own_Order()
        {
            using var context = CreateInMemoryDbContext();
            var order = new Order
            {
                Id = 1,
                OrderNumber = "BELAH-20261010-MYORDER1",
                UserId = 7,
                CustomerName = "Alice Customer",
                Email = "alice@example.com",
                Phone = "+27123456789",
                Address = "456 Rose St",
                City = "Durban",
                Province = "KwaZulu-Natal",
                PostalCode = "4001",
                TotalAmount = 1800m,
                Status = "Pending",
                PaymentStatus = "Pending",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = 1,
                        ProductId = 5,
                        ProductName = "HD Lace Frontal",
                        Quantity = 1,
                        UnitPrice = 1800m,
                        Subtotal = 1800m
                    }
                }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // Customer logged in with alice@example.com
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim(ClaimTypes.Name, "Alice"),
                new Claim(ClaimTypes.Email, "alice@example.com"),
                new Claim(ClaimTypes.NameIdentifier, "7")
            };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            var controller = new OrdersController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };

            var result = await controller.GetOrderByNumber("BELAH-20261010-MYORDER1");
            var okResult = Assert.IsType<OkObjectResult>(result);
            var orderDto = Assert.IsType<OrderResponseDto>(okResult.Value);

            Assert.Equal("Alice Customer", orderDto.CustomerName);
            Assert.Equal("456 Rose St", orderDto.Address);
        }

        [Fact]
        public async Task Customer_Cannot_View_Another_Customers_PII_Via_GetOrderByNumber()
        {
            using var context = CreateInMemoryDbContext();
            var order = new Order
            {
                Id = 1,
                OrderNumber = "BELAH-20261010-OTHER1",
                CustomerName = "Bob Smith",
                Email = "bob@example.com",
                Phone = "+27987654321",
                Address = "789 Pine Ave",
                City = "Johannesburg",
                Province = "Gauteng",
                PostalCode = "2000",
                TotalAmount = 3000m,
                Status = "Pending",
                PaymentStatus = "Pending",
                OrderItems = new List<OrderItem>()
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // Customer logged in with alice@example.com (different user)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim(ClaimTypes.Name, "Alice"),
                new Claim(ClaimTypes.Email, "alice@example.com")
            };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            var controller = new OrdersController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };

            var result = await controller.GetOrderByNumber("BELAH-20261010-OTHER1");
            var okResult = Assert.IsType<OkObjectResult>(result);
            // Must return privacy-safe public tracking DTO, NOT full OrderResponseDto
            var publicDto = Assert.IsType<PublicOrderTrackingDto>(okResult.Value);

            Assert.Equal("BELAH-20261010-OTHER1", publicDto.OrderNumber);
        }

        [Fact]
        public async Task GetMyOrders_Returns_Only_Authenticated_Users_Orders()
        {
            using var context = CreateInMemoryDbContext();
            context.Orders.AddRange(
                new Order
                {
                    Id = 1,
                    OrderNumber = "BELAH-20261010-ALICE1",
                    UserId = 1,
                    CustomerName = "Alice",
                    Email = "alice@example.com",
                    Phone = "123",
                    Address = "St 1",
                    City = "City",
                    Province = "Prov",
                    PostalCode = "0001",
                    TotalAmount = 1000m,
                    OrderItems = new List<OrderItem>()
                },
                new Order
                {
                    Id = 2,
                    OrderNumber = "BELAH-20261010-BOB1",
                    CustomerName = "Bob",
                    Email = "bob@example.com",
                    Phone = "456",
                    Address = "St 2",
                    City = "City",
                    Province = "Prov",
                    PostalCode = "0002",
                    TotalAmount = 2000m,
                    OrderItems = new List<OrderItem>()
                }
            );
            await context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim(ClaimTypes.Email, "alice@example.com"),
                new Claim(ClaimTypes.NameIdentifier, "1")
            };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            var controller = new OrdersController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };

            var result = await controller.GetMyOrders();
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var myOrders = Assert.IsAssignableFrom<IEnumerable<OrderResponseDto>>(okResult.Value).ToList();

            Assert.Single(myOrders);
            Assert.Equal("BELAH-20261010-ALICE1", myOrders[0].OrderNumber);
        }

    }
}
