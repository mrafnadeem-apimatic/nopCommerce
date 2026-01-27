using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.PayPal.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayPal.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class PaymentPayPalController : BasePaymentController
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentPayPalController"/> with required services.
    /// </summary>
    public PaymentPayPalController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Displays the PayPal payment plugin configuration view populated with the current store-scoped settings.
    /// </summary>
    /// <returns>An IActionResult rendering the PayPal Configure view with a ConfigurationModel containing the active store scope, setting values, and per-store override flags.</returns>
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var payPalSettings = await _settingService.LoadSettingAsync<PayPalPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            ClientId = payPalSettings.ClientId,
            ClientSecret = payPalSettings.ClientSecret,
            UseSandbox = payPalSettings.UseSandbox,
            AdditionalFee = payPalSettings.AdditionalFee,
            AdditionalFeePercentage = payPalSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope
        };

        if (storeScope > 0)
        {
            model.ClientId_OverrideForStore = await _settingService.SettingExistsAsync(payPalSettings, x => x.ClientId, storeScope);
            model.ClientSecret_OverrideForStore = await _settingService.SettingExistsAsync(payPalSettings, x => x.ClientSecret, storeScope);
            model.UseSandbox_OverrideForStore = await _settingService.SettingExistsAsync(payPalSettings, x => x.UseSandbox, storeScope);
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(payPalSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(payPalSettings, x => x.AdditionalFeePercentage, storeScope);
        }

        return View("~/Plugins/Payments.PayPal/Views/Configure.cshtml", model);
    }

    /// <summary>
    /// Persists PayPal configuration from the provided model for the active store scope and then redisplays the configuration page.
    /// </summary>
    /// <param name="model">ConfigurationModel containing PayPal client credentials, sandbox flag, additional fee values, and per-store override flags.</param>
    /// <returns>An IActionResult that redisplays the configuration view; when the model is invalid the view shows validation errors.</returns>
    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var payPalSettings = await _settingService.LoadSettingAsync<PayPalPaymentSettings>(storeScope);

        //save settings
        payPalSettings.ClientId = model.ClientId;
        payPalSettings.ClientSecret = model.ClientSecret;
        payPalSettings.UseSandbox = model.UseSandbox;
        payPalSettings.AdditionalFee = model.AdditionalFee;
        payPalSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.ClientId, model.ClientId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.ClientSecret, model.ClientSecret_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion
}

