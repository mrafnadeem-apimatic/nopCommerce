using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.PayPal.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPal.Components;

/// <summary>
/// Represents the PayPal payment info view component shown on the payment info step (if not skipped)
/// </summary>
public class PayPalViewComponent : NopViewComponent
{
    private readonly ILocalizationService _localizationService;
    private readonly IWorkContext _workContext;

    public PayPalViewComponent(ILocalizationService localizationService, IWorkContext workContext)
    {
        _localizationService = localizationService;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new PaymentInfoModel
        {
            //show the dedicated payment info message describing the redirect flow
            Description = await _localizationService.GetResourceAsync("Plugins.Payments.PayPal.PaymentInfo.Description", (await _workContext.GetWorkingLanguageAsync()).Id)
        };

        return View("~/Plugins/Payments.PayPal/Views/PaymentInfo.cshtml", model);
    }
}

