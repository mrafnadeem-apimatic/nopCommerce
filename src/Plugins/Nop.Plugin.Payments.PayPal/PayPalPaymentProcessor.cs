using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayPal.Components;
using Nop.Plugin.Payments.PayPal.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.PayPal;

/// <summary>
/// PayPal payment processor implementing the redirection-based flow using PayPal Orders API.
/// </summary>
public class PayPalPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderService _orderService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly PayPalOrdersService _payPalOrdersService;
    protected readonly PayPalPaymentSettings _payPalPaymentSettings;

    #endregion

    #region Ctor

    public PayPalPaymentProcessor(IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        IOrderService orderService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        PayPalOrdersService payPalOrdersService,
        PayPalPaymentSettings payPalPaymentSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
        _orderService = orderService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _payPalOrdersService = payPalOrdersService;
        _payPalPaymentSettings = payPalPaymentSettings;
    }

    #endregion

    #region Methods

    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var result = new ProcessPaymentResult
        {
            NewPaymentStatus = PaymentStatus.Pending
        };

        return Task.FromResult(result);
    }

    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        var order = postProcessPaymentRequest.Order ?? throw new ArgumentNullException(nameof(postProcessPaymentRequest.Order));

        // create PayPal order for the full order total in the customer currency
        // The return/cancel routes are exposed under /Plugins/PaymentPayPal/* by RouteProvider.
        var storeLocation = _webHelper.GetStoreLocation();
        var returnUrl = $"{storeLocation}Plugins/PaymentPayPal/Return?orderGuid={order.OrderGuid}";
        var cancelUrl = $"{storeLocation}Plugins/PaymentPayPal/Cancel?orderGuid={order.OrderGuid}";

        var (success, payPalOrderId, approvalUrl, error) = await _payPalOrdersService.CreateOrderAsync(order.OrderTotal, order.CustomerCurrencyCode, returnUrl, cancelUrl);

        if (!success || string.IsNullOrEmpty(approvalUrl))
            throw new NopException($"Error creating PayPal order. {error}");

        //store PayPal order id for reference
        order.AuthorizationTransactionId = payPalOrderId;
        await _orderService.UpdateOrderAsync(order);

        //redirect to PayPal approval URL
        _httpContextAccessor.HttpContext.Response.Redirect(approvalUrl);
    }

    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        //always available (additional business rules could be added here)
        return Task.FromResult(false);
    }

    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(
            cart,
            _payPalPaymentSettings.AdditionalFee,
            _payPalPaymentSettings.AdditionalFeePercentage);
    }

    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        //online capture is handled via PayPal return callback, not through this method
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported directly. Capture occurs automatically after PayPal approval." } });
    }

    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
    }

    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
    }

    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        //allow re-posting as long as order is still pending
        return Task.FromResult(order.PaymentStatus == PaymentStatus.Pending);
    }

    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        //no additional data to validate
        return Task.FromResult<IList<string>>(new List<string>());
    }

    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        //no additional payment info is required, everything is handled by PayPal
        return Task.FromResult(new ProcessPaymentRequest());
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayPal/Configure";
    }

    public Type GetPublicViewComponent()
    {
        return typeof(PayPalViewComponent);
    }

    public override async Task InstallAsync()
    {
        var settings = new PayPalPaymentSettings
        {
            UseSandbox = true
        };
        await _settingService.SaveSettingAsync(settings);

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.PayPal.Fields.ClientId"] = "Client ID",
            ["Plugins.Payments.PayPal.Fields.ClientId.Hint"] = "Enter your PayPal REST API client ID.",
            ["Plugins.Payments.PayPal.Fields.ClientSecret"] = "Client secret",
            ["Plugins.Payments.PayPal.Fields.ClientSecret.Hint"] = "Enter your PayPal REST API client secret.",
            ["Plugins.Payments.PayPal.Fields.UseSandbox"] = "Use Sandbox",
            ["Plugins.Payments.PayPal.Fields.UseSandbox.Hint"] = "Check to use the PayPal Sandbox environment.",
            ["Plugins.Payments.PayPal.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.PayPal.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.PayPal.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.PayPal.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.PayPal.PaymentMethodDescription"] = "Pay with PayPal. You will be redirected to the PayPal website to complete your purchase.",
            ["Plugins.Payments.PayPal.PaymentInfo.Description"] = "After confirming the order you will be redirected to PayPal to securely complete your payment.",
            ["Plugins.Payments.PayPal.Errors.MissingToken"] = "PayPal token is missing. Please try again.",
            ["Plugins.Payments.PayPal.Errors.OrderMismatch"] = "PayPal order reference mismatch. Please try again or contact support."
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<PayPalPaymentSettings>();

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.PayPal");

        await base.UninstallAsync();
    }

    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.PayPal.PaymentMethodDescription");
    }

    #endregion

    #region Properties

    public bool SupportCapture => false;

    public bool SupportPartiallyRefund => false;

    public bool SupportRefund => false;

    public bool SupportVoid => false;

    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    /// <summary>
    /// Gets a value indicating whether we should skip the payment info step during checkout.
    /// For this PayPal redirection method we still want to show a short description
    /// on the payment info step, so we do not skip it.
    /// </summary>
    public bool SkipPaymentInfo => false;

    #endregion
}

