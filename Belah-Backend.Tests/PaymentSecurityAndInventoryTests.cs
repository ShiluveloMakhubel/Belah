using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Belah_Backend.Controllers;
using Belah_Backend.Data;
using Belah_Backend.DTOs;
using Belah_Backend.Models;
using Belah_Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;

namespace Belah_Backend.Tests;

public sealed class PaymentSecurityAndInventoryTests : IDisposable
{
    private readonly string _connectionString = $"Data Source=BelahTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10";
    private readonly SqliteConnection _anchor;

    public PaymentSecurityAndInventoryTests()
    {
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _anchor.Dispose();

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connectionString).Options;
        return new ApplicationDbContext(options);
    }

    private PaymentsController CreateController(ApplicationDbContext context, TestPaystack paystack, int? userId = null, string? role = null)
    {
        var claims = new List<Claim>();
        if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        var identity = new ClaimsIdentity(claims, "test");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "test-webhook-secret"
        }).Build();
        return new PaymentsController(context, paystack, NullLogger<PaymentsController>.Instance, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
    }

    private static async Task<(User user, Product product, Order order)> SeedOrder(
        ApplicationDbContext context, int? ownerId = null, string? capability = null, int stock = 8, int quantity = 2,
        string? reference = "BELAH-test-reference")
    {
        var user = new User { Username = "buyer", Email = "buyer@example.com", PasswordHash = "test-hash", Role = "Customer" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var product = new Product { Name = "Test product", Description = "Test", Price = 100m, StockQuantity = stock, IsActive = true };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var order = new Order
        {
            OrderNumber = $"BELAH-TEST-{Guid.NewGuid():N}", UserId = ownerId == -1 ? user.Id : ownerId,
            GuestCapabilityHash = capability == null ? null : Hash(capability),
            PaymentReference = reference, OrderDate = DateTime.UtcNow, Status = "Pending", PaymentStatus = "Pending",
            TotalAmount = 100m * quantity, CustomerName = "Buyer", Email = "buyer@example.com", Phone = "000",
            Address = "1 Main", City = "City", Province = "Province", PostalCode = "0000",
            OrderItems = new List<OrderItem> { new() { ProductId = product.Id, ProductName = product.Name, UnitPrice = 100m, Quantity = quantity, Subtotal = 100m * quantity } }
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return (user, product, order);
    }

    [Fact]
    public async Task Database_Rejects_Duplicate_NonNull_Product_Skus()
    {
        using var context = CreateContext();
        context.Products.Add(new Product { Name = "First", Description = "Test", Price = 10m, Sku = "SKU-UNIQUE" });
        await context.SaveChangesAsync();
        context.Products.Add(new Product { Name = "Duplicate", Description = "Test", Price = 10m, Sku = "SKU-UNIQUE" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private PaymentsController CreateWebhookController(ApplicationDbContext context, TestPaystack paystack, string payload, string signature)
    {
        var controller = CreateController(context, paystack);
        var request = controller.ControllerContext.HttpContext.Request;
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        request.Headers["x-paystack-signature"] = signature;
        return controller;
    }

    private static string SignWebhook(string payload, string secret) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    [Fact]
    public async Task Webhook_Rejects_Invalid_Signatures_Without_Verifying_Payment()
    {
        using var context = CreateContext();
        var service = new TestPaystack();
        var payload = "{\"event\":\"charge.success\",\"data\":{\"reference\":\"not-trusted\"}}";
        var controller = CreateWebhookController(context, service, payload, new string('0', 128));

        var response = await controller.HandlePaystackWebhook(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        Assert.Equal(0, service.VerifyCalls);
        Assert.Empty(await context.EmailNotifications.ToListAsync());
    }

    [Fact]
    public async Task Webhook_Rejects_Malformed_Signed_Json()
    {
        using var context = CreateContext();
        var service = new TestPaystack();
        const string payload = "{malformed";
        var controller = CreateWebhookController(context, service, payload, SignWebhook(payload, "test-webhook-secret"));

        var response = await controller.HandlePaystackWebhook(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(0, service.VerifyCalls);
    }

    [Fact]
    public async Task Signed_Webhook_Uses_Verification_And_Duplicate_Delivery_Is_Idempotent()
    {
        using var context = CreateContext();
        var (_, product, order) = await SeedOrder(context, ownerId: -1, stock: 5, quantity: 2);
        var service = new TestPaystack { Verification = VerifyData(order) };
        var payload = JsonSerializer.Serialize(new { @event = "charge.success", data = new { reference = order.PaymentReference } });
        var signature = SignWebhook(payload, "test-webhook-secret");

        var first = await CreateWebhookController(context, service, payload, signature).HandlePaystackWebhook(CancellationToken.None);
        var second = await CreateWebhookController(context, service, payload, signature).HandlePaystackWebhook(CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        Assert.IsType<OkObjectResult>(second);
        Assert.Equal(1, service.VerifyCalls);
        Assert.Equal(3, await context.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal("Paid", (await context.Orders.SingleAsync(o => o.Id == order.Id)).PaymentStatus);
        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal($"order-confirmation:{order.Id}", notification.IdempotencyKey);
        Assert.Equal(order.Email, notification.RecipientEmail);
        Assert.Equal("Pending", notification.Status);
    }

    [Fact]
    public async Task Signed_Webhook_Acknowledges_Unhandled_Event_Without_Payment_State_Changes()
    {
        using var context = CreateContext();
        var service = new TestPaystack();
        const string payload = "{\"event\":\"refund.processed\",\"data\":{}}";
        var controller = CreateWebhookController(context, service, payload, SignWebhook(payload, "test-webhook-secret"));

        var response = await controller.HandlePaystackWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(response);
        Assert.Equal(0, service.VerifyCalls);
    }

    [Fact]
    public async Task Customer_Cannot_Initialize_Another_Customers_Order()
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        var service = new TestPaystack();
        var controller = CreateController(context, service, userId: 999, role: "Customer");
        var result = await controller.InitializePayment(new InitializePaymentRequest { OrderNumber = order.OrderNumber });
        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, service.InitializeCalls);
        var verify = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });
        Assert.IsType<ForbidResult>(verify.Result);
        Assert.Equal(0, service.VerifyCalls);
    }

    [Fact]
    public async Task Guest_Capability_Is_Required_And_Valid_Capability_Allows_Initialization()
    {
        using var context = CreateContext();
        var capability = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var (_, _, order) = await SeedOrder(context, capability: capability, reference: null);
        var service = new TestPaystack();
        var controller = CreateController(context, service);

        var denied = await controller.InitializePayment(new InitializePaymentRequest { OrderNumber = order.OrderNumber, GuestCapability = "wrong" });
        Assert.IsType<ForbidResult>(denied.Result);
        var allowed = await controller.InitializePayment(new InitializePaymentRequest { OrderNumber = order.OrderNumber, GuestCapability = capability });
        Assert.IsType<OkObjectResult>(allowed.Result);
        Assert.Equal(1, service.InitializeCalls);
        Assert.False(string.IsNullOrWhiteSpace(service.LastInitialization?.Reference));
    }

    [Fact]
    public async Task Guest_Verification_Requires_Correct_Capability_And_Unknown_References_Are_Rejected()
    {
        using var context = CreateContext();
        var capability = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await SeedOrder(context, capability: capability);
        var service = new TestPaystack();
        var controller = CreateController(context, service);
        var denied = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = "BELAH-test-reference", GuestCapability = "wrong" });
        Assert.IsType<ForbidResult>(denied.Result);
        Assert.Equal(0, service.VerifyCalls);
        var unknown = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = "unknown", GuestCapability = capability });
        Assert.IsType<NotFoundObjectResult>(unknown.Result);
        service.Verification = VerifyData(await context.Orders.SingleAsync());
        var allowed = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = "BELAH-test-reference", GuestCapability = capability });
        Assert.IsType<OkObjectResult>(allowed.Result);
    }

    [Fact]
    public async Task Provider_Reference_Mismatch_Is_Rejected()
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        var service = new TestPaystack { Verification = VerifyData(order, reference: "different-reference") };
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");
        var result = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(19999, "ZAR")]
    [InlineData(20000, "USD")]
    public async Task Successful_But_Mismatched_Provider_Payment_Requires_Refund(int amount, string currency)
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        var service = new TestPaystack { Verification = VerifyData(order, amount: amount, currency: currency) };
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");
        var result = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });
        Assert.IsType<ConflictObjectResult>(result.Result);
        var saved = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal("RefundRequired", saved.PaymentStatus);
        Assert.NotNull(saved.PaystackTransactionId);
    }

    [Theory]
    [InlineData("pending", "Pending")]
    [InlineData("failed", "Failed")]
    public async Task Pending_Or_Failed_Provider_Status_Cannot_Mark_Order_Paid(string providerStatus, string expectedPaymentStatus)
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        var service = new TestPaystack { Verification = VerifyData(order, status: providerStatus) };
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");
        var result = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(expectedPaymentStatus, (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).PaymentStatus);
        Assert.Empty(await context.EmailNotifications.ToListAsync());
    }

    [Fact]
    public async Task Repeated_Successful_Verification_Deducts_Stock_Only_Once()
    {
        using var context = CreateContext();
        var (_, product, order) = await SeedOrder(context, ownerId: -1, stock: 5, quantity: 2);
        var service = new TestPaystack { Verification = VerifyData(order) };
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");
        Assert.IsType<OkObjectResult>((await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! })).Result);
        Assert.IsType<OkObjectResult>((await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! })).Result);
        Assert.Equal(3, await context.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(1, service.VerifyCalls);
        Assert.Equal(1, await context.EmailNotifications.CountAsync());
    }

    [Fact]
    public async Task Duplicate_Product_Lines_Aggregate_And_Insufficient_Stock_Is_Not_Hidden()
    {
        using var context = CreateContext();
        var (_, product, order) = await SeedOrder(context, ownerId: -1, stock: 3, quantity: 1);
        order.TotalAmount = 200m;
        order.OrderItems.Add(new OrderItem { ProductId = product.Id, ProductName = product.Name ?? "Test product", UnitPrice = 100m, Quantity = 1, Subtotal = 100m });
        await context.SaveChangesAsync();
        var service = new TestPaystack { Verification = VerifyData(order, amount: 20000) };
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");
        var result = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, await context.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());

        var (_, lowStock, shortOrder) = await SeedOrder(context, ownerId: -1, stock: 1, quantity: 2, reference: "short-order-ref");
        var shortService = new TestPaystack { Verification = VerifyData(shortOrder) };
        var shortController = CreateController(context, shortService, userId: shortOrder.UserId, role: "Customer");
        Assert.IsType<ConflictObjectResult>((await shortController.VerifyPayment(new VerifyPaymentRequest { Reference = shortOrder.PaymentReference! })).Result);
        Assert.Equal("RefundRequired", (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == shortOrder.Id)).PaymentStatus);
        Assert.Equal(1, await context.Products.Where(p => p.Id == lowStock.Id).Select(p => p.StockQuantity).SingleAsync());
    }

    [Fact]
    public async Task Order_Creation_Uses_Selected_Variant_Price_And_Rejects_Missing_Size()
    {
        using var context = CreateContext();
        var product = new Product { Name = "Fragrance", Description = "Test listing", Category = "Fragrances", Price = 90m, StockQuantity = 5, IsActive = true };
        product.Variants.Add(new ProductVariant { Name = "50 ml", Price = 125m, StockQuantity = 2, IsActive = true });
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var variant = await context.ProductVariants.SingleAsync();
        var controller = new OrdersController(context) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var request = new CreateOrderRequest
        {
            CustomerName = "Buyer", Email = "buyer@example.com", Phone = "0000000", Address = "1 Main",
            City = "City", Province = "Province", PostalCode = "0000",
            Items = new List<CreateOrderItemDto> { new() { ProductId = product.Id, VariantId = variant.Id, Quantity = 2 } }
        };
        var created = await controller.CreateOrder(request);
        var createdResult = Assert.IsType<CreatedAtActionResult>(created.Result);
        Assert.Equal(250m, Assert.IsType<CreateOrderResponseDto>(createdResult.Value).TotalAmount);
        var savedLine = await context.OrderItems.SingleAsync();
        Assert.Equal(variant.Id, savedLine.ProductVariantId);
        Assert.Equal("50 ml", savedLine.SelectedVariantName);
        Assert.Equal(125m, savedLine.UnitPrice);

        request.Items[0].VariantId = null;
        var missingSize = await controller.CreateOrder(request);
        Assert.IsType<BadRequestObjectResult>(missingSize.Result);
        Assert.Equal(1, await context.Orders.CountAsync());
    }

    [Fact]
    public async Task Duplicate_Successful_Payment_Deducts_Selected_Variant_Stock_Once()
    {
        using var context = CreateContext();
        var (_, product, order) = await SeedOrder(context, ownerId: -1, stock: 5, quantity: 2);
        var variant = new ProductVariant { ProductId = product.Id, Name = "75 ml", Price = 100m, StockQuantity = 5, IsActive = true };
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();
        order.OrderItems.Single().ProductVariantId = variant.Id;
        order.OrderItems.Single().SelectedVariantName = variant.Name;
        await context.SaveChangesAsync();
        var service = new TestPaystack { Verification = VerifyData(order) };
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");

        Assert.IsType<OkObjectResult>((await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! })).Result);
        Assert.IsType<OkObjectResult>((await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! })).Result);
        Assert.Equal(3, await context.ProductVariants.Where(v => v.Id == variant.Id).Select(v => v.StockQuantity).SingleAsync());
        Assert.Equal(3, await context.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(1, service.VerifyCalls);
    }

    [Fact]
    public async Task Missing_Product_Is_Recorded_For_Refund_Handling()
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        order.OrderItems.First().ProductId = null;
        var service = new TestPaystack { Verification = VerifyData(order) };
        await context.SaveChangesAsync();
        var controller = CreateController(context, service, userId: order.UserId, role: "Customer");
        Assert.IsType<ConflictObjectResult>((await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! })).Result);
        Assert.Equal("RefundRequired", (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).PaymentStatus);
    }

    [Fact]
    public async Task Admin_Cannot_Set_Payment_Or_Order_Status_To_Paid()
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        var controller = new OrdersController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }, "test")) }
            }
        };
        var orderStatus = await controller.UpdateOrderStatus(order.Id, new UpdateOrderStatusDto { Status = "Paid" });
        Assert.IsType<BadRequestObjectResult>(orderStatus);
        var paymentStatus = await controller.UpdateOrderStatus(order.Id, new UpdateOrderStatusDto { Status = "Processing", PaymentStatus = "Paid" });
        Assert.IsType<BadRequestObjectResult>(paymentStatus);
    }

    [Fact]
    public async Task Concurrent_Verifications_Cannot_Deduct_The_Same_Order_Twice()
    {
        using (var setup = CreateContext()) await SeedOrder(setup, ownerId: -1, stock: 5, quantity: 2);
        using var read = CreateContext();
        var order = await read.Orders.AsNoTracking().SingleAsync();
        var gate = new TwoCallGate();
        var provider = new TestPaystack { Verification = VerifyData(order), VerifyGate = gate };
        using var leftDb = CreateContext();
        using var rightDb = CreateContext();
        var left = CreateController(leftDb, provider, userId: order.UserId, role: "Customer");
        var right = CreateController(rightDb, provider, userId: order.UserId, role: "Customer");
        var results = await Task.WhenAll(
            left.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! }),
            right.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! }));
        Assert.Contains(results, r => r.Result is OkObjectResult);
        Assert.Equal(3, await read.Products.Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal("Paid", (await read.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(1, await read.EmailNotifications.CountAsync());
    }

    [Fact]
    public async Task Concurrent_Webhook_And_Owner_Verification_Create_One_Confirmation()
    {
        int ownerId;
        Order order;
        using (var setup = CreateContext())
        {
            var (owner, _, createdOrder) = await SeedOrder(setup, ownerId: -1, stock: 5, quantity: 2);
            ownerId = owner.Id;
            order = createdOrder;
        }
        var gate = new TwoCallGate();
        var provider = new TestPaystack { Verification = VerifyData(order), VerifyGate = gate };
        var payload = JsonSerializer.Serialize(new { @event = "charge.success", data = new { reference = order.PaymentReference } });
        var signature = SignWebhook(payload, "test-webhook-secret");
        using var verifyDb = CreateContext();
        using var webhookDb = CreateContext();
        var verifyController = CreateController(verifyDb, provider, ownerId, "Customer");
        var webhookController = CreateWebhookController(webhookDb, provider, payload, signature);

        var verifyTask = verifyController.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });
        var webhookTask = webhookController.HandlePaystackWebhook(CancellationToken.None);
        await Task.WhenAll((Task)verifyTask, webhookTask);

        using var read = CreateContext();
        var verifyResponse = await verifyTask;
        var webhookResponse = await webhookTask;
        Assert.True(verifyResponse.Result is OkObjectResult || webhookResponse is OkObjectResult);
        Assert.Equal("Paid", (await read.Orders.SingleAsync(o => o.Id == order.Id)).PaymentStatus);
        Assert.Equal(1, await read.EmailNotifications.CountAsync(n => n.OrderId == order.Id));
        Assert.Equal(3, await read.Products.Select(p => p.StockQuantity).SingleAsync());
    }

    [Fact]
    public async Task Expired_Paid_Provider_Response_Does_Not_Queue_Confirmation()
    {
        using var context = CreateContext();
        var (_, _, order) = await SeedOrder(context, ownerId: -1);
        order.PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();
        var provider = new TestPaystack { Verification = VerifyData(order) };
        var controller = CreateController(context, provider, userId: order.UserId, role: "Customer");

        var result = await controller.VerifyPayment(new VerifyPaymentRequest { Reference = order.PaymentReference! });

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("RefundRequired", (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).PaymentStatus);
        Assert.Empty(await context.EmailNotifications.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_Orders_Cannot_Oversell_The_Last_Unit()
    {
        int firstOwner;
        int secondOwner;
        int productId;
        Order first;
        Order second;
        using (var setup = CreateContext())
        {
            var (firstUser, product, firstOrder) = await SeedOrder(setup, ownerId: -1, stock: 1, quantity: 1);
            var secondUser = new User { Username = "second", Email = "second@example.com", PasswordHash = "test-hash", Role = "Customer" };
            setup.Users.Add(secondUser);
            await setup.SaveChangesAsync();
            second = new Order
            {
                OrderNumber = $"BELAH-TEST-{Guid.NewGuid():N}", UserId = secondUser.Id,
                PaymentReference = "last-unit-second-ref", OrderDate = DateTime.UtcNow, Status = "Pending", PaymentStatus = "Pending",
                TotalAmount = 100m, CustomerName = "Second buyer", Email = secondUser.Email, Phone = "000",
                Address = "2 Main", City = "City", Province = "Province", PostalCode = "0000",
                OrderItems = new List<OrderItem> { new() { ProductId = product.Id, ProductName = "Test product", UnitPrice = 100m, Quantity = 1, Subtotal = 100m } }
            };
            setup.Orders.Add(second);
            await setup.SaveChangesAsync();
            firstOwner = firstUser.Id;
            secondOwner = secondUser.Id;
            productId = product.Id;
            first = firstOrder;
        }

        var gate = new TwoCallGate();
        var provider = new TestPaystack
        {
            VerifyGate = gate,
            VerificationFactory = reference => new PaystackVerifyData
            {
                Id = Math.Abs((long)StringComparer.Ordinal.GetHashCode(reference)) + 1,
                Reference = reference,
                Amount = 10000,
                Currency = "ZAR",
                Status = "success",
                Customer = new PaystackCustomer { Email = "buyer@example.com" }
            }
        };
        using var leftDb = CreateContext();
        using var rightDb = CreateContext();
        var left = CreateController(leftDb, provider, firstOwner, "Customer");
        var right = CreateController(rightDb, provider, secondOwner, "Customer");
        var results = await Task.WhenAll(
            left.VerifyPayment(new VerifyPaymentRequest { Reference = first.PaymentReference! }),
            right.VerifyPayment(new VerifyPaymentRequest { Reference = second.PaymentReference! }));

        using var verify = CreateContext();
        var statuses = await verify.Orders.AsNoTracking().Select(o => o.PaymentStatus).ToListAsync();
        Assert.Equal(0, await verify.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync());
        Assert.Single(statuses, status => status == "Paid");
        Assert.Single(statuses, status => status == "RefundRequired");
        Assert.Contains(results, result => result.Result is OkObjectResult);
    }

    [Fact]
    public async Task Paystack_Initialization_Uses_Server_Secret_And_Validates_Response()
    {
        var handler = new FakeHttpHandler();
        var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "test_secret_only",
            ["Paystack:CallbackUrl"] = "https://belah.example.test/checkout"
        }).Build();
        var service = new PaystackService(http, config, NullLogger<PaystackService>.Instance);
        var result = await service.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = "buyer@example.com", Amount = 20000, Currency = "ZAR", Reference = "stored-reference"
        });
        Assert.NotNull(result);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test_secret_only", handler.AuthorizationParameter);
        Assert.Equal("stored-reference", handler.RequestBody!.RootElement.GetProperty("reference").GetString());
        Assert.Equal(20000, handler.RequestBody.RootElement.GetProperty("amount").GetInt64());
    }

    private static PaystackVerifyData VerifyData(Order order, string? reference = null, int? amount = null, string currency = "ZAR", string status = "success") => new()
    {
        Id = RandomNumberGenerator.GetInt32(1000, int.MaxValue),
        Reference = reference ?? order.PaymentReference!,
        Amount = amount ?? (long)(order.TotalAmount * 100m),
        Currency = currency,
        Status = status,
        Customer = new PaystackCustomer { Email = order.Email }
    };

    private sealed class TestPaystack : IPaystackService
    {
        private int _verifyCalls;
        public int InitializeCalls { get; private set; }
        public int VerifyCalls => _verifyCalls;
        public PaystackInitializeRequest? LastInitialization { get; private set; }
        public PaystackVerifyData? Verification { get; set; }
        public Func<string, PaystackVerifyData?>? VerificationFactory { get; set; }
        public TwoCallGate? VerifyGate { get; set; }
        public Task<PaystackInitializeData?> InitializeTransactionAsync(PaystackInitializeRequest request)
        {
            InitializeCalls++;
            LastInitialization = request;
            return Task.FromResult<PaystackInitializeData?>(new PaystackInitializeData
            {
                AuthorizationUrl = "https://checkout.example.test/session", AccessCode = "access", Reference = request.Reference
            });
        }
        public async Task<PaystackVerifyData?> VerifyTransactionAsync(string reference)
        {
            Interlocked.Increment(ref _verifyCalls);
            if (VerifyGate != null) await VerifyGate.Arrive();
            return VerificationFactory?.Invoke(reference) ?? Verification;
        }
    }

    private sealed class TwoCallGate
    {
        private int _calls;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Arrive()
        {
            if (Interlocked.Increment(ref _calls) == 2) _both.TrySetResult();
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public JsonDocument? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var reference = RequestBody.RootElement.GetProperty("reference").GetString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    status = true,
                    data = new { authorization_url = "https://checkout.example.test/session", access_code = "access", reference }
                }))
            };
        }
    }
}
