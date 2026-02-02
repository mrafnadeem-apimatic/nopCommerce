using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.PayPal.Models;
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

    public PaymentPayPalController(ILocalizationService localizationService,
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

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
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

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var payPalSettings = await _settingService.LoadSettingAsync<PayPalPaymentSettings>(storeScope);

        payPalSettings.ClientId = model.ClientId;
        payPalSettings.ClientSecret = model.ClientSecret;
        payPalSettings.UseSandbox = model.UseSandbox;
        payPalSettings.AdditionalFee = model.AdditionalFee;
        payPalSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.ClientId, model.ClientId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.ClientSecret, model.ClientSecret_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(payPalSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion
}

