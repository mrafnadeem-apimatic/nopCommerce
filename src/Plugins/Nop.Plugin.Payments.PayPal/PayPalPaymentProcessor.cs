using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayPal.Components;
using Nop.Plugin.Payments.PayPal.Models;
using Nop.Plugin.Payments.PayPal.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.PayPal;

/// <summary>
/// Represents PayPal payment processor
/// </summary>
public class PayPalPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
    protected readonly IOrderService _orderService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly PayPalPaymentSettings _payPalPaymentSettings;
    protected readonly PayPalHttpClient _payPalHttpClient;

    #endregion

    #region Ctor

    public PayPalPaymentProcessor(
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        ILogger logger,
        IOrderService orderService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        PayPalPaymentSettings payPalPaymentSettings,
        PayPalHttpClient payPalHttpClient)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
        _logger = logger;
        _orderService = orderService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _payPalPaymentSettings = payPalPaymentSettings;
        _payPalHttpClient = payPalHttpClient;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Process a payment
    /// </summary>
    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var result = new ProcessPaymentResult
        {
            //PayPal payment will be captured after the customer approves it
            NewPaymentStatus = PaymentStatus.Pending
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
    /// </summary>
    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(postProcessPaymentRequest);
        ArgumentNullException.ThrowIfNull(postProcessPaymentRequest.Order);

        var order = postProcessPaymentRequest.Order;

        var returnUrl = $"{_webHelper.GetStoreLocation()}PayPal/Return?orderId={order.Id}";
        var cancelUrl = $"{_webHelper.GetStoreLocation()}PayPal/Cancel?orderId={order.Id}";

        var (orderId, approvalUrl) = await _payPalHttpClient.CreateOrderAsync(order, returnUrl, cancelUrl);

        //store PayPal order identifier for reference
        order.AuthorizationTransactionId = orderId;
        await _orderService.UpdateOrderAsync(order);

        await _logger.InsertLogAsync(LogLevel.Information,
            $"Redirecting to PayPal for order #{order.Id} (PayPal order {orderId})",
            approvalUrl);

        var httpContext = _httpContextAccessor.HttpContext
                           ?? throw new NopException("HTTP context is not available");

        httpContext.Response.Redirect(approvalUrl);
    }

    /// <summary>
    /// Returns a value indicating whether payment method should be hidden during checkout
    /// </summary>
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        //visible by default, additional rules can be added if required
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets additional handling fee
    /// </summary>
    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
            _payPalPaymentSettings.AdditionalFee, _payPalPaymentSettings.AdditionalFeePercentage);
    }

    /// <summary>
    /// Captures payment
    /// </summary>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        //capturing is handled via PayPal API when the customer is redirected back,
        //so admin-side capture is not supported
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        //full API-based refunds can be implemented later if needed
        return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
    }

    /// <summary>
    /// Voids a payment
    /// </summary>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        //API-based voids can be implemented later if needed
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
    }

    /// <summary>
    /// Process recurring payment
    /// </summary>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        //recurring payments through PayPal are not supported in this basic integration
        return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    /// <summary>
    /// Cancels a recurring payment
    /// </summary>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    /// <summary>
    /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
    /// </summary>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        //allow re-posting for pending PayPal payments
        return Task.FromResult(order.PaymentStatus == PaymentStatus.Pending);
    }

    /// <summary>
    /// Validate payment form
    /// </summary>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        //no payment form is displayed for PayPal (redirection method)
        return Task.FromResult<IList<string>>(new List<string>());
    }

    /// <summary>
    /// Get payment information
    /// </summary>
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        //no additional payment info is required for PayPal
        return Task.FromResult(new ProcessPaymentRequest());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayPal/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    public Type GetPublicViewComponent()
    {
        return typeof(PayPalPaymentInfoViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new PayPalPaymentSettings
        {
            UseSandbox = true
        };
        await _settingService.SaveSettingAsync(settings);

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.PayPal.Instructions"] = "Configure your PayPal REST API credentials. Customers will be redirected to PayPal to approve the payment.",
            ["Plugins.Payments.PayPal.Fields.ClientId"] = "Client ID",
            ["Plugins.Payments.PayPal.Fields.ClientId.Hint"] = "Enter your PayPal REST API client ID.",
            ["Plugins.Payments.PayPal.Fields.ClientSecret"] = "Client secret",
            ["Plugins.Payments.PayPal.Fields.ClientSecret.Hint"] = "Enter your PayPal REST API client secret.",
            ["Plugins.Payments.PayPal.Fields.UseSandbox"] = "Use sandbox",
            ["Plugins.Payments.PayPal.Fields.UseSandbox.Hint"] = "Check to use PayPal sandbox environment for testing.",
            ["Plugins.Payments.PayPal.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.PayPal.Fields.AdditionalFee.Hint"] = "Enter an additional fee to charge your customers.",
            ["Plugins.Payments.PayPal.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.PayPal.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.PayPal.PaymentMethodDescription"] = "Pay with PayPal"
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    public override async Task UninstallAsync()
    {
        //settings
        await _settingService.DeleteSettingAsync<PayPalPaymentSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.PayPal");

        await base.UninstallAsync();
    }

    /// <summary>
    /// Gets a payment method description that will be displayed on checkout pages in the public store
    /// </summary>
    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.PayPal.PaymentMethodDescription");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether capture is supported
    /// </summary>
    public bool SupportCapture => false;

    /// <summary>
    /// Gets a value indicating whether partial refund is supported
    /// </summary>
    public bool SupportPartiallyRefund => false;

    /// <summary>
    /// Gets a value indicating whether refund is supported
    /// </summary>
    public bool SupportRefund => false;

    /// <summary>
    /// Gets a value indicating whether void is supported
    /// </summary>
    public bool SupportVoid => false;

    /// <summary>
    /// Gets a recurring payment type of payment method
    /// </summary>
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    /// <summary>
    /// Gets a payment method type
    /// </summary>
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    /// <summary>
    /// Gets a value indicating whether we should display a payment information page for this plugin
    /// </summary>
    public bool SkipPaymentInfo => true;

    #endregion
}


