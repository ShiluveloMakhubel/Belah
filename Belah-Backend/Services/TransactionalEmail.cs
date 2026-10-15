using System.Globalization;
using System.Net;
using System.Text;
using Belah_Backend.Models;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Belah_Backend.Services;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string SecureSocketOptions { get; set; } = "StartTls";
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 &&
        MailboxAddress.TryParse(FromAddress, out _) &&
        (string.IsNullOrWhiteSpace(Username) == string.IsNullOrWhiteSpace(Password));

    public MailKit.Security.SecureSocketOptions GetSecurityMode() => SecureSocketOptions switch
    {
        "StartTls" => MailKit.Security.SecureSocketOptions.StartTls,
        "StartTlsWhenAvailable" => MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable,
        "SslOnConnect" => MailKit.Security.SecureSocketOptions.SslOnConnect,
        "None" => MailKit.Security.SecureSocketOptions.None,
        _ => throw new InvalidOperationException("Email:Smtp:SecureSocketOptions must be StartTls, StartTlsWhenAvailable, SslOnConnect, or None.")
    };
}

public sealed class PurchaseConfirmationOptions
{
    public string? SupportEmail { get; set; }
    public string? StorefrontBaseUrl { get; set; }
}

public sealed record OutboundEmail(string Recipient, string Subject, string HtmlBody, string TextBody);

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(OutboundEmail email, CancellationToken cancellationToken);
}

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<SmtpEmailOptions> _options;

    public MailKitEmailSender(IOptionsMonitor<SmtpEmailOptions> options) => _options = options;

    public bool IsConfigured => _options.CurrentValue.IsConfigured;

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.IsConfigured) throw new InvalidOperationException("SMTP is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName?.Trim() ?? "Belah", options.FromAddress!.Trim()));
        message.To.Add(MailboxAddress.Parse(email.Recipient));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(options.Host!, options.Port, options.GetSecurityMode(), cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.Username))
            await client.AuthenticateAsync(options.Username, options.Password!, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

public static class PurchaseConfirmationTemplate
{
    private static readonly CultureInfo SouthAfricanCulture = CultureInfo.GetCultureInfo("en-ZA");

    public static OutboundEmail Render(EmailNotification notification, Order order, PurchaseConfirmationOptions options)
    {
        var orderNumber = order.OrderNumber;
        var subject = $"Belah order confirmed — {orderNumber}";
        var rowsHtml = new StringBuilder();
        var rowsText = new StringBuilder();
        foreach (var item in order.OrderItems.OrderBy(item => item.Id))
        {
            var name = item.ProductName + FormatOptions(item);
            rowsHtml.Append("<tr><td>").Append(Encode(name)).Append("</td><td>")
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                .Append(Encode(FormatMoney(item.UnitPrice, notification.Currency))).Append("</td><td>")
                .Append(Encode(FormatMoney(item.Subtotal, notification.Currency))).Append("</td></tr>");
            rowsText.Append("- ").Append(name).Append(" | Qty ").Append(item.Quantity.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatMoney(item.UnitPrice, notification.Currency)).Append(" each | ")
                .Append(FormatMoney(item.Subtotal, notification.Currency)).AppendLine();
        }

        var customerName = order.CustomerName.Trim();
        var supportEmail = string.IsNullOrWhiteSpace(options.SupportEmail) ? null : options.SupportEmail.Trim();
        var trackingUrl = BuildTrackingUrl(options.StorefrontBaseUrl, orderNumber);
        var address = string.Join(", ", new[] { order.Address, order.City, order.Province, order.PostalCode }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        var date = order.OrderDate.ToString("d MMMM yyyy 'at' HH:mm", SouthAfricanCulture);
        var total = FormatMoney(order.TotalAmount, notification.Currency);
        var supportHtml = supportEmail == null ? string.Empty : $"<p>Questions? Contact <a href=\"mailto:{Encode(supportEmail)}\">{Encode(supportEmail)}</a>.</p>";
        var trackingHtml = trackingUrl == null ? string.Empty : $"<p><a href=\"{Encode(trackingUrl)}\">View your order status</a></p>";

        var html = $$"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Order confirmed</title>
<style>body{margin:0;background:#f5f3ef;color:#25231f;font:16px/1.5 Arial,sans-serif}.wrap{max-width:640px;margin:24px auto;padding:0 14px}.card{background:#fff;border:1px solid #e8e4dc;border-radius:14px;overflow:hidden}.brand{background:#171614;color:#fff;padding:22px 28px;font-weight:bold;letter-spacing:.16em}.content{padding:28px}h1{font-size:26px;margin:0 0 8px}.muted{color:#625f59}table{width:100%;border-collapse:collapse;margin:22px 0}th,td{text-align:left;padding:10px 6px;border-bottom:1px solid #e8e4dc;vertical-align:top}th{font-size:12px;text-transform:uppercase;color:#625f59}.total{text-align:right;font-size:19px;font-weight:bold}.delivery{background:#f8f7f4;padding:14px;border-radius:8px}a{color:#80652d}@media(max-width:480px){.content{padding:20px 16px}th,td{font-size:13px;padding:8px 3px} }</style></head>
<body><main class="wrap"><section class="card"><header class="brand">BELAH</header><div class="content"><h1>Order confirmed</h1>
<p>Thank you{{(customerName.Length > 0 ? $", {Encode(customerName)}" : string.Empty)}}. Your payment has been verified and your order is confirmed.</p>
<p><strong>Order:</strong> {{Encode(orderNumber)}}<br><strong>Order date:</strong> {{Encode(date)}}</p>
<table><thead><tr><th>Product</th><th>Qty</th><th>Each</th><th>Subtotal</th></tr></thead><tbody>{{rowsHtml}}</tbody></table>
<p class="total">Total: {{Encode(total)}}</p><div class="delivery"><strong>Delivery address</strong><br>{{Encode(address)}}</div>
{{trackingHtml}}{{supportHtml}}<p class="muted">Thank you for shopping with Belah.</p></div></section></main></body></html>
""";

        var text = new StringBuilder()
            .AppendLine("BELAH — ORDER CONFIRMED")
            .AppendLine()
            .Append("Thank you").Append(customerName.Length > 0 ? $", {customerName}" : string.Empty).AppendLine(". Your payment has been verified and your order is confirmed.")
            .Append("Order: ").AppendLine(orderNumber)
            .Append("Order date: ").AppendLine(date)
            .AppendLine()
            .AppendLine("Items:").Append(rowsText)
            .Append("Order total: ").AppendLine(total)
            .Append("Delivery address: ").AppendLine(address)
            .AppendLine();
        if (trackingUrl != null) text.Append("Track your order: ").AppendLine(trackingUrl);
        if (supportEmail != null) text.Append("Customer support: ").AppendLine(supportEmail);
        text.AppendLine("Thank you for shopping with Belah.");

        return new OutboundEmail(notification.RecipientEmail, subject, html, text.ToString());
    }

    public static string FormatMoney(decimal amount, string currency) =>
        $"{currency} {amount.ToString("N2", SouthAfricanCulture)}";

    private static string? BuildTrackingUrl(string? storefrontBaseUrl, string orderNumber)
    {
        if (!Uri.TryCreate(storefrontBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))) return null;
        var basePath = baseUri.ToString().TrimEnd('/');
        return $"{basePath}/track-order?orderNumber={Uri.EscapeDataString(orderNumber)}";
    }

    private static string FormatOptions(OrderItem item)
    {
        var selected = new[] { item.SelectedVariantName, item.SelectedColor, item.SelectedStorage }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim());
        var values = string.Join(" / ", selected);
        return values.Length == 0 ? string.Empty : $" ({values})";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
