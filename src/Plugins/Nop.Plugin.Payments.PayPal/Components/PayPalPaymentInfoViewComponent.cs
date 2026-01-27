using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPal.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPal.Components;

/// <summary>
/// View component for displaying PayPal payment information during checkout
/// </summary>
public class PayPalPaymentInfoViewComponent : NopViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
    {
        var model = new PaymentInfoModel();

        return Task.FromResult<IViewComponentResult>(View("~/Plugins/Payments.PayPal/Views/PaymentInfo.cshtml", model));
    }
}


