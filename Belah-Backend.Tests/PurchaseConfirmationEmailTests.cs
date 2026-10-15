using Belah_Backend.Controllers;
using Belah_Backend.Data;
using Belah_Backend.Models;
using Belah_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Belah_Backend.Tests;

public sealed class PurchaseConfirmationEmailTests : IDisposable
{
    private readonly string _connectionString = $"Data Source=BelahEmailTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10";
    private readonly SqliteConnection _anchor;

    public PurchaseConfirmationEmailTests()
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

    private static PurchaseConfirmationOptions ConfirmationOptions() => new()
    {
        SupportEmail = "help@belah.example", StorefrontBaseUrl = "https://belah.example"
    };

    private EmailOutboxProcessor CreateProcessor(ApplicationDbContext context, IEmailSender sender) => new(context, sender,
        new OptionsMonitorStub<PurchaseConfirmationOptions>(ConfirmationOptions()),
        NullLogger<EmailOutboxProcessor>.Instance);

    private async Task<(Order order, EmailNotification notification)> AddPaidOrderWithNotification(
        ApplicationDbContext context, string productName = "Vanilla fragrance")
    {
        var product = new Product { Name = productName, Description = "Current catalogue name", Price = 999m, StockQuantity = 4 };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var order = new Order
        {
            OrderNumber = $"BELAH-TEST-{Guid.NewGuid():N}", OrderDate = new DateTime(2026, 10, 13, 10, 30, 0, DateTimeKind.Utc),
            Status = "Processing", PaymentStatus = "Paid", PaymentReference = "test-paid-reference", PaystackTransactionId = 1001,
            TotalAmount = 250m, CustomerName = "A <Buyer>", Email = "buyer@example.test", Phone = "000",
            Address = "1 <Main> Street", City = "Cape Town", Province = "Western Cape", PostalCode = "8000",
            OrderItems = new List<OrderItem> { new()
            {
                ProductId = product.Id, ProductName = "Saved <Rose> perfume", SelectedVariantName = "50 ml",
                Quantity = 2, UnitPrice = 125m, Subtotal = 250m
            } }
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        var notification = new EmailNotification
        {
            OrderId = order.Id, IdempotencyKey = $"order-confirmation:{order.Id}", RecipientEmail = order.Email,
            Currency = "ZAR", Status = "Pending", CreatedAtUtc = DateTime.UtcNow, NextAttemptAtUtc = DateTime.UtcNow
        };
        context.EmailNotifications.Add(notification);
        await context.SaveChangesAsync();
        return (order, notification);
    }

    [Fact]
    public void Receipt_Uses_Saved_Line_Items_Zar_Total_Tracking_Link_And_Escaped_Data()
    {
        var order = new Order
        {
            OrderNumber = "BELAH-20261013-ABC", OrderDate = new DateTime(2026, 10, 13, 10, 30, 0, DateTimeKind.Utc),
            CustomerName = "<Buyer>", Email = "buyer@example.test", Address = "<Main Street>", City = "Cape Town",
            Province = "Western Cape", PostalCode = "8000", TotalAmount = 250m,
            OrderItems = new List<OrderItem> { new()
            {
                ProductName = "<Rose perfume>", SelectedVariantName = "50 ml", Quantity = 2,
                UnitPrice = 125m, Subtotal = 250m
            } }
        };
        var notification = new EmailNotification { RecipientEmail = order.Email, Currency = "ZAR" };
        var message = PurchaseConfirmationTemplate.Render(notification, order, ConfirmationOptions());

        Assert.Contains("ZAR 250,00", message.HtmlBody);
        Assert.Contains("ZAR 250,00", message.TextBody);
        Assert.Contains("<Rose perfume> (50 ml)", message.TextBody);
        Assert.Contains("Cape Town", message.TextBody);
        Assert.Contains("https://belah.example/track-order?orderNumber=BELAH-20261013-ABC", message.TextBody);
        Assert.Contains("&lt;Rose perfume&gt;", message.HtmlBody);
        Assert.DoesNotContain("<Rose perfume>", message.HtmlBody);
        Assert.Contains("&lt;Main Street&gt;", message.HtmlBody);
        Assert.DoesNotContain("test-paid-reference", message.HtmlBody);
        Assert.DoesNotContain("GuestCapability", message.HtmlBody);
    }

    [Fact]
    public async Task Missing_Smtp_Configuration_Leaves_Notification_Pending()
    {
        using var context = CreateContext();
        await AddPaidOrderWithNotification(context);
        var sender = new MailKitEmailSender(new OptionsMonitorStub<SmtpEmailOptions>(new SmtpEmailOptions()));

        var delivered = await CreateProcessor(context, sender).ProcessAvailableAsync();

        Assert.Equal(0, delivered);
        Assert.Equal("Pending", (await context.EmailNotifications.SingleAsync()).Status);
        Assert.Equal("Paid", (await context.Orders.SingleAsync()).PaymentStatus);
    }

    [Fact]
    public async Task Delivery_Failure_Leaves_Order_Paid_And_Schedules_Bounded_Retry()
    {
        using var context = CreateContext();
        await AddPaidOrderWithNotification(context);
        var sender = new TestEmailSender { Failure = new IOException("mail transport failed") };

        await CreateProcessor(context, sender).ProcessAvailableAsync();

        using var verify = CreateContext();
        var notification = await verify.EmailNotifications.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", notification.Status);
        Assert.Equal(1, notification.Attempts);
        Assert.Equal("IOException", notification.LastErrorCode);
        Assert.True(notification.NextAttemptAtUtc > DateTime.UtcNow);
        Assert.Equal("Paid", (await verify.Orders.AsNoTracking().SingleAsync()).PaymentStatus);
    }

    [Fact]
    public async Task Transient_Delivery_Failure_Is_Retried_And_Then_Marked_Sent()
    {
        using var context = CreateContext();
        await AddPaidOrderWithNotification(context);
        var sender = new TestEmailSender { FailuresRemaining = 1 };
        var processor = CreateProcessor(context, sender);
        var product = await context.Products.SingleAsync();
        product.Price = 5m;
        await context.SaveChangesAsync();

        await processor.ProcessAvailableAsync();
        await context.EmailNotifications.ExecuteUpdateAsync(update => update.SetProperty(n => n.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        await processor.ProcessAvailableAsync();

        using var verify = CreateContext();
        var notification = await verify.EmailNotifications.AsNoTracking().SingleAsync();
        Assert.Equal("Sent", notification.Status);
        Assert.Equal(2, notification.Attempts);
        Assert.NotNull(notification.SentAtUtc);
        Assert.Single(sender.SuccessfulMessages);
        Assert.Contains("Saved <Rose> perfume (50 ml)", sender.SuccessfulMessages[0].TextBody);
        Assert.Contains("ZAR 125,00 each", sender.SuccessfulMessages[0].TextBody);
    }

    [Fact]
    public async Task Historical_Paid_Order_Does_Not_Get_A_Notification_Without_A_New_Payment_Transition()
    {
        using var context = CreateContext();
        var product = new Product { Name = "Old item", Description = "Historical item", Price = 10m, StockQuantity = 0 };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        context.Orders.Add(new Order
        {
            OrderNumber = "BELAH-HISTORICAL-PAID", Status = "Completed", PaymentStatus = "Paid", TotalAmount = 10m,
            CustomerName = "Past buyer", Email = "past@example.test", Phone = "000", Address = "Old address",
            City = "City", Province = "Province", PostalCode = "0000",
            OrderItems = new List<OrderItem> { new() { ProductId = product.Id, ProductName = "Old item", Quantity = 1, UnitPrice = 10m, Subtotal = 10m } }
        });
        await context.SaveChangesAsync();
        var sender = new TestEmailSender();

        await CreateProcessor(context, sender).ProcessAvailableAsync();

        Assert.Empty(await context.EmailNotifications.ToListAsync());
        Assert.Empty(sender.SuccessfulMessages);
    }

    [Fact]
    public async Task Admin_Can_Manually_Requeue_A_Failed_Notification_Without_Exposing_Recipient()
    {
        using var context = CreateContext();
        var (_, notification) = await AddPaidOrderWithNotification(context);
        notification.Status = "Failed";
        notification.Attempts = EmailOutboxProcessor.MaximumAttempts;
        notification.LastErrorCode = "SmtpException";
        await context.SaveChangesAsync();
        var controller = new AdminEmailNotificationsController(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.List("Failed", CancellationToken.None);
        var listed = Assert.IsType<OkObjectResult>(result.Result);
        var status = Assert.IsAssignableFrom<IReadOnlyList<EmailNotificationStatusDto>>(listed.Value).Single();
        Assert.Equal(notification.Id, status.Id);
        Assert.DoesNotContain("buyer@example.test", System.Text.Json.JsonSerializer.Serialize(status));
        Assert.IsType<AcceptedResult>(await controller.Retry(notification.Id, CancellationToken.None));
        using var verify = CreateContext();
        var requeued = await verify.EmailNotifications.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", requeued.Status);
        Assert.Equal(0, requeued.Attempts);

        var authorize = typeof(AdminEmailNotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Admin", authorize.Roles);
    }

    private sealed class TestEmailSender(bool configured = true) : IEmailSender
    {
        public bool IsConfigured { get; } = configured;
        public int SendCalls { get; private set; }
        public int FailuresRemaining { get; set; }
        public Exception? Failure { get; set; }
        public List<OutboundEmail> SuccessfulMessages { get; } = new();

        public Task SendAsync(OutboundEmail email, CancellationToken cancellationToken)
        {
            SendCalls++;
            if (Failure != null || FailuresRemaining > 0)
            {
                if (FailuresRemaining > 0) FailuresRemaining--;
                throw Failure ?? new IOException("temporary test transport failure");
            }
            SuccessfulMessages.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
