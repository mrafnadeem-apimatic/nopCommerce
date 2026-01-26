using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPal.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPal.Components;

/// <summary>
/// View component for displaying PayPal payment information during checkout
/// </summary>
public class PayPalPaymentInfoViewComponent : NopViewComponent
{
    /// <summary>
    /// Renders the PayPal payment information view for checkout using a new PaymentInfoModel.
    /// </summary>
    /// <returns>An <see cref="IViewComponentResult"/> that renders the PayPal payment information view with a <see cref="PaymentInfoModel"/>.</returns>
    public Task<IViewComponentResult> InvokeAsync()
    {
        var model = new PaymentInfoModel();

        return Task.FromResult<IViewComponentResult>(View("~/Plugins/Payments.PayPal/Views/PaymentInfo.cshtml", model));
    }
}

