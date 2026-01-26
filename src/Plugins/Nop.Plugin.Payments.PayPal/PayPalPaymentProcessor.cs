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

    /// <summary>
    /// Initializes a new instance of the <see cref="PayPalPaymentProcessor"/> with the required services and configuration.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current HTTP context (used when redirecting to PayPal).</param>
    /// <param name="localizationService">Service for retrieving localized resources.</param>
    /// <param name="logger">Logger for recording informational and error messages.</param>
    /// <param name="orderService">Service for managing orders.</param>
    /// <param name="orderTotalCalculationService">Service for calculating order totals and payment additional fees.</param>
    /// <param name="settingService">Service for reading and saving plugin settings.</param>
    /// <param name="webHelper">Helper for building store and URL information.</param>
    /// <param name="payPalPaymentSettings">PayPal-specific configuration settings for the plugin.</param>
    /// <param name="payPalHttpClient">HTTP client used to create and manage PayPal orders.</param>
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
    /// <summary>
    /// Initiates processing of the payment request and leaves the payment in Pending state for PayPal approval.
    /// </summary>
    /// <returns>A ProcessPaymentResult with NewPaymentStatus set to PaymentStatus.Pending.</returns>
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
    /// <summary>
    /// Creates a PayPal order for the given order, saves the PayPal order identifier on the order, and redirects the HTTP response to PayPal's approval URL.
    /// </summary>
    /// <param name="postProcessPaymentRequest">Request containing the order to post-process.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postProcessPaymentRequest"/> or its Order is null.</exception>
    /// <exception cref="NopException">Thrown when the current HTTP context is not available and a redirect cannot be performed.</exception>
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
    /// <summary>
    /// Determines whether the PayPal payment method should be hidden for the specified shopping cart.
    /// </summary>
    /// <param name="cart">The shopping cart items to evaluate payment-method visibility for.</param>
    /// <returns>`true` if the payment method should be hidden for the provided cart, `false` otherwise.</returns>
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        //visible by default, additional rules can be added if required
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets additional handling fee
    /// <summary>
    /// Calculates the additional payment handling fee for the specified shopping cart using the configured PayPal fee settings.
    /// </summary>
    /// <param name="cart">The shopping cart items to calculate the fee for.</param>
    /// <returns>The additional handling fee amount.</returns>
    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
            _payPalPaymentSettings.AdditionalFee, _payPalPaymentSettings.AdditionalFeePercentage);
    }

    /// <summary>
    /// Captures payment
    /// <summary>
    /// Indicates that capture is not supported for this payment method; capture is performed via PayPal after buyer approval and cannot be initiated here.
    /// </summary>
    /// <param name="capturePaymentRequest">The capture request (ignored).</param>
    /// <returns>A CapturePaymentResult containing an error message stating that capture is not supported.</returns>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        //capturing is handled via PayPal API when the customer is redirected back,
        //so admin-side capture is not supported
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
    }

    /// <summary>
    /// Refunds a payment
    /// <summary>
    /// Indicates that refunds are not supported by this payment processor.
    /// </summary>
    /// <returns>A <see cref="RefundPaymentResult"/> whose <c>Errors</c> indicate that refunds are not supported.</returns>
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        //full API-based refunds can be implemented later if needed
        return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
    }

    /// <summary>
    /// Voids a payment
    /// <summary>
    /// Signals that voiding a previously authorized payment is not supported by this processor.
    /// </summary>
    /// <returns>A <see cref="VoidPaymentResult"/> containing an error indicating void is not supported.</returns>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        //API-based voids can be implemented later if needed
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
    }

    /// <summary>
    /// Process recurring payment
    /// <summary>
    /// Processes a recurring payment request; this processor does not support recurring payments and will return an error.
    /// </summary>
    /// <param name="processPaymentRequest">The recurring payment request to process.</param>
    /// <returns>A <see cref="ProcessPaymentResult"/> containing an error message indicating recurring payments are not supported.</returns>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        //recurring payments through PayPal are not supported in this basic integration
        return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    /// <summary>
    /// Cancels a recurring payment
    /// <summary>
    /// Indicates that cancelling recurring payments is not supported and returns an error result.
    /// </summary>
    /// <param name="cancelPaymentRequest">The request details for cancelling a recurring payment (ignored).</param>
    /// <returns>The <see cref="CancelRecurringPaymentResult"/> containing an error message that recurring payments are not supported.</returns>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    /// <summary>
    /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
    /// <summary>
    /// Determines whether post-processing (re-posting) of the payment is allowed for the specified order.
    /// </summary>
    /// <param name="order">The order to check for re-posting eligibility.</param>
    /// <returns>`true` if the order's payment status is Pending, `false` otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="order"/> is null.</exception>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        //allow re-posting for pending PayPal payments
        return Task.FromResult(order.PaymentStatus == PaymentStatus.Pending);
    }

    /// <summary>
    /// Validate payment form
    /// <summary>
    /// Validate payment form input for the PayPal redirection payment method.
    /// </summary>
    /// <param name="form">The submitted form data (ignored for this redirection-based method).</param>
    /// <returns>An empty list of validation error messages.</returns>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        //no payment form is displayed for PayPal (redirection method)
        return Task.FromResult<IList<string>>(new List<string>());
    }

    /// <summary>
    /// Get payment information
    /// <summary>
    /// Provide payment information required to process the payment; for PayPal redirection this always returns an empty request.
    /// </summary>
    /// <param name="form">The submitted form collection from the storefront; ignored for this payment method.</param>
    /// <returns>A ProcessPaymentRequest instance with no payment-specific data.</returns>
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        //no additional payment info is required for PayPal
        return Task.FromResult(new ProcessPaymentRequest());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// <summary>
    /// Gets the admin URL for the PayPal plugin configuration page.
    /// </summary>
    /// <returns>The full URL to the plugin's configuration page in the admin area.</returns>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayPal/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// <summary>
    /// Gets the view component type used to render this payment method's information in the public storefront.
    /// </summary>
    /// <returns>The <see cref="Type"/> of the view component that displays the payment method information.</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(PayPalPaymentInfoViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// <summary>
    /// Installs the PayPal payment plugin by creating default plugin settings and registering required localization resources.
    /// </summary>
    /// <remarks>
    /// Persists default PayPal settings (enabling sandbox by default), adds the plugin's locale resources used for UI labels and hints, and then calls the base installation routine.
    /// </remarks>
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
    /// <summary>
    /// Deletes the PayPal plugin settings and localization resources (under "Plugins.Payments.PayPal"), then invokes the base uninstallation logic.
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
    /// <summary>
    /// Gets the localized description for the PayPal payment method.
    /// </summary>
    /// <returns>The localized payment method description string.</returns>
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

