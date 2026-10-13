using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Belah_Backend.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Belah_Backend.Services;

public sealed class PaystackService : IPaystackService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaystackService> _logger;

    public PaystackService(HttpClient httpClient, IConfiguration configuration, ILogger<PaystackService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri("https://api.paystack.co/");
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<PaystackInitializeData?> InitializeTransactionAsync(PaystackInitializeRequest payload)
    {
        if (!HasSecretKey()) return null;
        var callbackUrl = _configuration["Paystack:CallbackUrl"];
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var callbackUri) ||
            (callbackUri.Scheme != Uri.UriSchemeHttps &&
             !(callbackUri.Scheme == Uri.UriSchemeHttp && (callbackUri.Host == "localhost" || callbackUri.Host == "127.0.0.1"))))
        {
            _logger.LogError("Paystack:CallbackUrl is missing or is not a secure absolute URL.");
            return null;
        }
        payload.CallbackUrl = callbackUri.ToString();

        try
        {
            using var request = CreateRequest(HttpMethod.Post, "transaction/initialize");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<PaystackInitializeResponse>(JsonOptions);
            var data = result?.Data;
            var validAuthorizationUrl = Uri.TryCreate(data?.AuthorizationUrl, UriKind.Absolute, out var authorizationUri) &&
                authorizationUri.Scheme == Uri.UriSchemeHttps;
            if (result?.Status != true || data == null ||
                !validAuthorizationUrl ||
                string.IsNullOrWhiteSpace(data.AccessCode) ||
                !string.Equals(data.Reference, payload.Reference, StringComparison.Ordinal))
            {
                _logger.LogWarning("Paystack returned an invalid transaction initialization response.");
                return null;
            }
            return data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Paystack transaction initialization request failed.");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Paystack transaction initialization timed out.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Paystack returned an unreadable initialization response.");
            return null;
        }
    }

    public async Task<PaystackVerifyData?> VerifyTransactionAsync(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !HasSecretKey()) return null;
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"transaction/verify/{Uri.EscapeDataString(reference)}");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<PaystackVerifyResponse>(JsonOptions);
            if (result?.Status != true || result.Data == null) return null;
            return result.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Paystack transaction verification request failed.");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Paystack transaction verification timed out.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Paystack returned an unreadable verification response.");
            return null;
        }
    }

    private bool HasSecretKey()
    {
        if (!string.IsNullOrWhiteSpace(_configuration["Paystack:SecretKey"])) return true;
        _logger.LogError("Paystack:SecretKey is not configured.");
        return false;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["Paystack:SecretKey"]);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
