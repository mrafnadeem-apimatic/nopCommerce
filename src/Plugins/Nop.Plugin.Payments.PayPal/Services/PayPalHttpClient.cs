using System;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.PayPal.Services;

/// <summary>
/// Represents a helper HTTP client for communicating with PayPal APIs
/// </summary>
public class PayPalHttpClient
{
    #region Fields

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly PayPalPaymentSettings _settings;

    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);
    private string _accessToken;
    private DateTime? _accessTokenExpiresAt;

    #endregion

    #region Ctor

    public PayPalHttpClient(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        PayPalPaymentSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings;
    }

    #endregion

    #region Utilities

    protected virtual string GetApiBaseUrl()
    {
        return _settings.UseSandbox
            ? PayPalDefaults.SandboxApiBaseUrl
            : PayPalDefaults.LiveApiBaseUrl;
    }

    protected virtual async Task<string> GetAccessTokenAsync()
    {
        // Fast path: return cached token if it exists and hasn't expired
        if (!string.IsNullOrEmpty(_accessToken) &&
            _accessTokenExpiresAt.HasValue &&
            _accessTokenExpiresAt.Value > DateTime.UtcNow)
        {
            return _accessToken;
        }

        await _tokenSemaphore.WaitAsync();
        try
        {
            // Double-check under the lock to avoid redundant refresh
            if (!string.IsNullOrEmpty(_accessToken) &&
                _accessTokenExpiresAt.HasValue &&
                _accessTokenExpiresAt.Value > DateTime.UtcNow)
            {
                return _accessToken;
            }

            if (string.IsNullOrEmpty(_settings.ClientId))
                throw new NopException("PayPal client ID is not set");

            if (string.IsNullOrEmpty(_settings.ClientSecret))
                throw new NopException("PayPal client secret is not set");

            var client = _httpClientFactory.CreateClient(PayPalDefaults.HttpClientName);
            client.BaseAddress = new Uri(GetApiBaseUrl());

            var credentials = $"{_settings.ClientId}:{_settings.ClientSecret}";
            var credentialsBytes = Encoding.UTF8.GetBytes(credentials);
            var authHeader = Convert.ToBase64String(credentialsBytes);

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await _logger.InsertLogAsync(LogLevel.Error, "PayPal OAuth error", content);
                throw new NopException("PayPal OAuth error");
            }

            using var document = JsonDocument.Parse(content);
            _accessToken = document.RootElement.GetProperty("access_token").GetString();

            // Parse token lifetime (expires_in is in seconds) and compute expiration with a safety margin
            if (document.RootElement.TryGetProperty("expires_in", out var expiresInElement) &&
                expiresInElement.TryGetInt32(out var expiresInSeconds))
            {
                var effectiveLifetimeSeconds = Math.Max(0, expiresInSeconds - 60); // 60 sec safety margin
                _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(effectiveLifetimeSeconds);
            }
            else
            {
                // Fallback if expires_in is missing: use a conservative default
                _accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(5);
            }

            return _accessToken;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Create PayPal order for the specified nopCommerce order
    /// </summary>
    /// <param name="order">Nop order</param>
    /// <param name="returnUrl">Return URL</param>
    /// <param name="cancelUrl">Cancel URL</param>
    /// <returns>PayPal order identifier and approval URL</returns>
    public virtual async Task<(string orderId, string approvalUrl)> CreateOrderAsync(Order order, string returnUrl, string cancelUrl)
    {
        ArgumentNullException.ThrowIfNull(order);

        var client = _httpClientFactory.CreateClient(PayPalDefaults.HttpClientName);
        client.BaseAddress = new Uri(GetApiBaseUrl());

        var accessToken = await GetAccessTokenAsync();

        var requestBody = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = order.CustomOrderNumber,
                    amount = new
                    {
                        currency_code = order.CustomerCurrencyCode,
                        value = order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture)
                    }
                }
            },
            application_context = new
            {
                return_url = returnUrl,
                cancel_url = cancelUrl
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var json = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            await _logger.InsertLogAsync(LogLevel.Error,
                $"Error creating PayPal order for nop order #{order.Id}",
                content);
            throw new NopException("Error creating PayPal order");
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var orderId = root.GetProperty("id").GetString();

        string approvalUrl = null;
        if (root.TryGetProperty("links", out var linksElement) && linksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linksElement.EnumerateArray())
            {
                var rel = link.GetProperty("rel").GetString();
                if (string.Equals(rel, "approve", StringComparison.InvariantCultureIgnoreCase))
                {
                    approvalUrl = link.GetProperty("href").GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(approvalUrl))
            throw new NopException("PayPal order or approval URL not found in response");

        return (orderId, approvalUrl);
    }

    /// <summary>
    /// Capture PayPal order
    /// </summary>
    /// <param name="payPalOrderId">PayPal order id (token)</param>
    /// <returns>True if captured successfully; otherwise false</returns>
    public virtual async Task<bool> CaptureOrderAsync(string payPalOrderId)
    {
        if (string.IsNullOrEmpty(payPalOrderId))
            return false;

        var client = _httpClientFactory.CreateClient(PayPalDefaults.HttpClientName);
        client.BaseAddress = new Uri(GetApiBaseUrl());

        var accessToken = await GetAccessTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v2/checkout/orders/{payPalOrderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // PayPal requires a JSON content type on capture requests; send an empty JSON object
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            await _logger.InsertLogAsync(
                LogLevel.Error,
                $"Error capturing PayPal order {payPalOrderId}",
                content);
            return false;
        }

        // Verify that the capture status is COMPLETED in the response payload
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var captureCompleted = false;

            // Prefer top-level status if present
            if (root.TryGetProperty("status", out var statusProperty))
            {
                if (string.Equals(statusProperty.GetString(), "COMPLETED", StringComparison.OrdinalIgnoreCase))
                    captureCompleted = true;
            }

            // Fallback: inspect purchase_units[].payments.captures[].status
            if (!captureCompleted &&
                root.TryGetProperty("purchase_units", out var puElement) &&
                puElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var pu in puElement.EnumerateArray())
                {
                    if (!pu.TryGetProperty("payments", out var paymentsElement))
                        continue;

                    if (!paymentsElement.TryGetProperty("captures", out var capturesElement) ||
                        capturesElement.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var capture in capturesElement.EnumerateArray())
                    {
                        if (capture.TryGetProperty("status", out var capStatus) &&
                            string.Equals(capStatus.GetString(), "COMPLETED", StringComparison.OrdinalIgnoreCase))
                        {
                            captureCompleted = true;
                            break;
                        }
                    }

                    if (captureCompleted)
                        break;
                }
            }

            if (!captureCompleted)
            {
                await _logger.InsertLogAsync(
                    LogLevel.Error,
                    $"PayPal capture for order {payPalOrderId} did not complete successfully. Response status OK but capture status was not COMPLETED.",
                    content);
                return false;
            }
        }
        catch (Exception ex)
        {
            await _logger.InsertLogAsync(
                LogLevel.Error,
                $"Failed to parse PayPal capture response for order {payPalOrderId}: {ex.Message}",
                content);
            return false;
        }

        return true;
    }

    #endregion
}


