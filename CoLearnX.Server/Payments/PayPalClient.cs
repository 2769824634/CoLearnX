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

public record CreatePayPalOrderRequest(int CreditPackageId, string? ReturnUrl = null);

public record CreatePayPalOrderResponse(string OrderId, int CreditPackageId, decimal Amount, string Currency);

public record CapturePayPalOrderRequest(string OrderId);

// Sandbox order create/capture.
public interface IPayPalClient
{
    PayPalClientConfigDto GetPublicConfig();
    Task<string> CreateOrderAsync(decimal amount, string currency, string customId, string description, string? returnUrl, CancellationToken ct = default);
    Task<(bool Success, string Status, string? CaptureId)> CaptureOrderAsync(string orderId, CancellationToken ct = default);
}

public class PayPalClient(IHttpClientFactory httpClientFactory, Microsoft.Extensions.Options.IOptions<PayPalOptions> options) : IPayPalClient
{
    private readonly PayPalOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public PayPalClientConfigDto GetPublicConfig()
        => new(_options.ClientId, _options.Currency, _options.Mode, _options.IsConfigured);

    public async Task<string> CreateOrderAsync(decimal amount, string currency, string customId, string description, string? returnUrl, CancellationToken ct = default)
    {
        EnsureConfigured();
        var token = await GetAccessTokenAsync(ct);
        var client = httpClientFactory.CreateClient("PayPal");
        var safeReturn = PayPalReturnUrls.Normalize(returnUrl);

        var applicationContext = new Dictionary<string, string>
        {
            ["brand_name"] = "CoLearnX",
            ["user_action"] = "PAY_NOW",
            ["shipping_preference"] = "NO_SHIPPING",
        };
        if (safeReturn is not null)
        {
            applicationContext["return_url"] = safeReturn;
            applicationContext["cancel_url"] = safeReturn;
        }

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
            application_context = applicationContext,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v2/checkout/orders");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(payload);

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatPayPalFailure("create order", res.StatusCode, body));

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
        {
            if (body.Contains("ORDER_ALREADY_CAPTURED", StringComparison.OrdinalIgnoreCase))
                return (true, "COMPLETED", null);
            throw new InvalidOperationException(FormatPayPalFailure("capture", res.StatusCode, body));
        }

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString() ?? "UNKNOWN";
        string? captureId = null;
        if (doc.RootElement.TryGetProperty("purchase_units", out var units) && units.GetArrayLength() > 0)
        {
            var payments = units[0].GetProperty("payments");
            if (payments.TryGetProperty("captures", out var captures) && captures.GetArrayLength() > 0)
                captureId = captures[0].GetProperty("id").GetString();
        }

        return (status == "COMPLETED", status, captureId);
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

        await _tokenLock.WaitAsync(ct);
        try
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
                throw new InvalidOperationException(FormatPayPalFailure("OAuth", res.StatusCode, body));

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            _cachedToken = doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("PayPal OAuth returned no access_token.");
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 300;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string FormatPayPalFailure(string action, System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var message = root.TryGetProperty("message", out var messageEl) ? messageEl.GetString() : null;
            string? issue = null;
            if (root.TryGetProperty("details", out var details) && details.ValueKind == System.Text.Json.JsonValueKind.Array
                && details.GetArrayLength() > 0)
            {
                var first = details[0];
                issue = first.TryGetProperty("issue", out var issueEl) ? issueEl.GetString() : null;
                if (first.TryGetProperty("description", out var descriptionEl))
                    message = descriptionEl.GetString() ?? message;
            }

            var debugId = root.TryGetProperty("debug_id", out var debugEl) ? debugEl.GetString() : null;
            var detail = string.Join(" ", new[] { issue, name, message }.Where(part => !string.IsNullOrWhiteSpace(part)));
            if (string.Equals(issue, "TRANSACTION_REFUSED", StringComparison.OrdinalIgnoreCase))
                detail += " Payer funding was declined, or the sandbox Business account is not set to accept AUD.";
            else if (string.Equals(issue, "ORDER_NOT_APPROVED", StringComparison.OrdinalIgnoreCase))
                detail += " The buyer did not approve the PayPal order.";

            if (!string.IsNullOrWhiteSpace(debugId))
                detail = $"{detail} debug_id={debugId}";

            if (!string.IsNullOrWhiteSpace(detail))
                return $"PayPal {action} failed ({(int)status}): {detail}";
        }
        catch (System.Text.Json.JsonException)
        {
            /* raw body below */
        }

        return $"PayPal {action} failed ({(int)status}): {body}";
    }
}

public static class PayPalReturnUrls
{
    public static string? Normalize(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var local = uri.Host is "localhost" or "127.0.0.1";
        var tunnel = uri.Host.EndsWith(".devtunnels.ms", StringComparison.OrdinalIgnoreCase);
        if (!local && !tunnel)
            return null;
        if (!local && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        if (!uri.AbsolutePath.StartsWith("/member/payment", StringComparison.OrdinalIgnoreCase))
            return null;

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
