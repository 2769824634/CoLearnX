namespace CoLearnX.Server.Payments;

// Config section name is "PayPal" (user-secrets / appsettings)
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    public string Mode { get; set; } = "Sandbox";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
    public string Currency { get; set; } = "AUD";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public record PayPalClientConfigDto(string ClientId, string Currency, string Mode, bool Enabled);

public record CreatePayPalOrderRequest(int CreditPackageId);

public record CreatePayPalOrderResponse(string OrderId, int CreditPackageId, decimal Amount, string Currency);

public record CapturePayPalOrderRequest(string OrderId);

// Sandbox order create/capture.
public interface IPayPalClient
{
    PayPalClientConfigDto GetPublicConfig();
    Task<string> CreateOrderAsync(decimal amount, string currency, string customId, string description, CancellationToken ct = default);
    Task<(bool Success, string Status, string? CaptureId)> CaptureOrderAsync(string orderId, CancellationToken ct = default);
}

public class PayPalClient(IHttpClientFactory httpClientFactory, Microsoft.Extensions.Options.IOptions<PayPalOptions> options) : IPayPalClient
{
    private readonly PayPalOptions _options = options.Value;
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public PayPalClientConfigDto GetPublicConfig()
        => new(_options.ClientId, _options.Currency, _options.Mode, _options.IsConfigured);

    public async Task<string> CreateOrderAsync(decimal amount, string currency, string customId, string description, CancellationToken ct = default)
    {
        EnsureConfigured();
        var token = await GetAccessTokenAsync(ct);
        var client = httpClientFactory.CreateClient("PayPal");

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    custom_id = customId,
                    description,
                    amount = new
                    {
                        currency_code = currency,
                        value = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    },
                },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v2/checkout/orders");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(payload);

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal create order failed: {(int)res.StatusCode} {body}");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var orderId = doc.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(orderId))
            throw new InvalidOperationException("PayPal create order returned no id.");
        return orderId;
    }

    public async Task<(bool Success, string Status, string? CaptureId)> CaptureOrderAsync(string orderId, CancellationToken ct = default)
    {
        EnsureConfigured();
        var token = await GetAccessTokenAsync(ct);
        var client = httpClientFactory.CreateClient("PayPal");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v2/checkout/orders/{orderId}/capture");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal capture failed: {(int)res.StatusCode} {body}");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString() ?? "UNKNOWN";
        string? captureId = null;
        if (doc.RootElement.TryGetProperty("purchase_units", out var units) && units.GetArrayLength() > 0)
        {
            var payments = units[0].GetProperty("payments");
            if (payments.TryGetProperty("captures", out var captures) && captures.GetArrayLength() > 0)
                captureId = captures[0].GetProperty("id").GetString();
        }

        return (status is "COMPLETED" or "APPROVED", status, captureId);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException("PayPal is not configured. Set PayPal:ClientId and PayPal:ClientSecret (user-secrets).");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        var client = httpClientFactory.CreateClient("PayPal");
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v1/oauth2/token");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
        });

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal OAuth failed: {(int)res.StatusCode} {body}");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        _cachedToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("PayPal OAuth returned no access_token.");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 300;
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        return _cachedToken;
    }
}
