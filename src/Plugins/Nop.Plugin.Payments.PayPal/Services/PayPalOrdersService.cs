using System.Globalization;
using Microsoft.Extensions.Logging;
using Nop.Services.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Plugin.Payments.PayPal.Services;

/// <summary>
/// Encapsulates calls to PayPal Orders API using the PayPal Server SDK.
/// </summary>
public class PayPalOrdersService
{
    #region Fields

    protected readonly PayPalPaymentSettings _settings;
    protected readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    protected readonly ILogger _logger;

    #endregion

    #region Ctor

    public PayPalOrdersService(PayPalPaymentSettings settings, ILogger logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _settings = settings;
        _logger = logger;
        _configuration = configuration;
    }

    #endregion

    #region Utilities

    protected PaypalServerSdkClient CreateClient()
    {
        // prefer plugin settings, but allow overriding from configuration for quick testing
        var clientId = _settings.ClientId;
        var clientSecret = _settings.ClientSecret;
        var useSandbox = _settings.UseSandbox;

        // temporary appsettings-based override: "PayPal:ClientId", "PayPal:ClientSecret", "PayPal:UseSandbox"
        clientId ??= _configuration["PayPal:ClientId"];
        clientSecret ??= _configuration["PayPal:ClientSecret"];

        var configUseSandbox = _configuration["PayPal:UseSandbox"];
        if (!string.IsNullOrEmpty(configUseSandbox) && bool.TryParse(configUseSandbox, out var parsedUseSandbox))
            useSandbox = parsedUseSandbox;

        // SDK logging is potentially sensitive (bodies/headers), so make it opt-in
        var enableSdkLogging = false;
        var configEnableLogging = _configuration["PayPal:EnableSdkLogging"];
        if (!string.IsNullOrEmpty(configEnableLogging) && bool.TryParse(configEnableLogging, out var parsedEnableLogging))
            enableSdkLogging = parsedEnableLogging;

        var environment = useSandbox
            ? PaypalServerSdk.Standard.Environment.Sandbox
            : PaypalServerSdk.Standard.Environment.Production;

        var builder = new PaypalServerSdkClient.Builder()
            .ClientCredentialsAuth(
                new ClientCredentialsAuthModel.Builder(
                        clientId,
                        clientSecret
                    )
                    .Build())
            .Environment(environment);

        // Configure SDK logging based on flag:
        // - when enabled: log at Information level with bodies and headers
        // - when disabled: minimal logging at Error level, no bodies/headers
        if (enableSdkLogging)
        {
            builder = builder.LoggingConfig(config => config
                .LogLevel(LogLevel.Information)
                .RequestConfig(reqConfig => reqConfig.Body(true))
                .ResponseConfig(respConfig => respConfig.Headers(true)));
        }
        else
        {
            builder = builder.LoggingConfig(config => config
                .LogLevel(LogLevel.Error));
        }

        return builder.Build();
    }

    /// <summary>
    /// Get the number of minor-unit decimal digits for a given ISO 4217 currency code.
    /// Default is 2; override for currencies like JPY (0) or TND (3).
    /// </summary>
    /// <param name="currencyCode">ISO 4217 currency code (e.g. USD, JPY).</param>
    /// <returns>Minor unit digit count.</returns>
    protected virtual int GetCurrencyMinorUnit(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return 2;

        currencyCode = currencyCode.ToUpperInvariant();

        // Zero-decimal currencies
        switch (currencyCode)
        {
            case "JPY":
            case "HUF":
            case "TWD":
            case "KRW":
                return 0;
        }

        // Three-decimal currencies
        switch (currencyCode)
        {
            case "BHD":
            case "IQD":
            case "JOD":
            case "KWD":
            case "LYD":
            case "OMR":
            case "TND":
                return 3;
        }

        // Default: 2 decimal places
        return 2;
    }

    #endregion

    #region Methods

    public async Task<(bool Success, string PayPalOrderId, string ApprovalUrl, string Error)> CreateOrderAsync(
        decimal orderTotal,
        string currencyCode,
        string returnUrl,
        string cancelUrl)
    {
        // validate we have credentials either from settings or configuration
        var clientId = _settings.ClientId ?? _configuration["PayPal:ClientId"];
        var clientSecret = _settings.ClientSecret ?? _configuration["PayPal:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return (false, null, null, "PayPal is not configured. Provide ClientId and ClientSecret either in plugin settings or under PayPal:ClientId/PayPal:ClientSecret in appsettings.");

        var minorUnits = GetCurrencyMinorUnit(currencyCode);
        var roundedTotal = Math.Round(orderTotal, minorUnits, MidpointRounding.AwayFromZero);
        var amountValue = roundedTotal.ToString($"F{minorUnits}", CultureInfo.InvariantCulture);

        var client = CreateClient();
        var ordersController = client.OrdersController;

        var createOrderInput = new CreateOrderInput
        {
            Body = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Capture,
                PurchaseUnits = new List<PurchaseUnitRequest>
                {
                    new()
                    {
                        Amount = new AmountWithBreakdown
                        {
                            CurrencyCode = currencyCode,
                            MValue = amountValue
                        }
                    }
                },
                ApplicationContext = new OrderApplicationContext
                {
                    ReturnUrl = returnUrl,
                    CancelUrl = cancelUrl
                }
            },
            Prefer = "return=representation"
        };

        try
        {
            ApiResponse<Order> result = await ordersController.CreateOrderAsync(createOrderInput);
            var order = result.Data;

            var approvalUrl = order.Links?
                .FirstOrDefault(l => string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase))
                ?.Href;

            if (string.IsNullOrEmpty(approvalUrl))
                return (false, order.Id, null, "PayPal order created but approval URL is missing.");

            return (true, order.Id, approvalUrl, null);
        }
        catch (ApiException ex)
        {
            var message = ex.Message;

            if (ex is ErrorException errorException)
            {
                message = $"{errorException.Name}: {errorException.Message} (DebugId: {errorException.DebugId})";
            }

            await _logger.ErrorAsync(message, ex);

            return (false, null, null, message);
        }
    }

    public class CaptureResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string CaptureId { get; set; }
        public string Error { get; set; }
    }

    public async Task<CaptureResult> CaptureOrderAsync(string payPalOrderId)
    {
        var clientId = _settings.ClientId ?? _configuration["PayPal:ClientId"];
        var clientSecret = _settings.ClientSecret ?? _configuration["PayPal:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return new CaptureResult { Success = false, Error = "PayPal is not configured. Provide ClientId and ClientSecret either in plugin settings or under PayPal:ClientId/PayPal:ClientSecret in appsettings." };

        var client = CreateClient();
        var ordersController = client.OrdersController;

        var captureOrderInput = new CaptureOrderInput
        {
            Id = payPalOrderId,
            Prefer = "return=representation"
        };

        try
        {
            ApiResponse<Order> result = await ordersController.CaptureOrderAsync(captureOrderInput);
            var order = result.Data;

            string firstCaptureId = null;

            if (order.PurchaseUnits != null)
            {
                foreach (var pu in order.PurchaseUnits)
                {
                    if (pu.Payments?.Captures != null)
                    {
                        var capture = pu.Payments.Captures.FirstOrDefault();
                        if (capture != null)
                        {
                            firstCaptureId = capture.Id;
                            break;
                        }
                    }
                }
            }

            return new CaptureResult
            {
                Success = order.Status == OrderStatus.Completed,
                Status = order.Status?.ToString(),
                CaptureId = firstCaptureId,
                Error = null
            };
        }
        catch (ApiException ex)
        {
            var message = ex.Message;

            if (ex is ErrorException errorException)
            {
                message = $"{errorException.Name}: {errorException.Message} (DebugId: {errorException.DebugId})";
            }

            await _logger.ErrorAsync(message, ex);

            return new CaptureResult
            {
                Success = false,
                Error = message
            };
        }
    }

    #endregion
}

