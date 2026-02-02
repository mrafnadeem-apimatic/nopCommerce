using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayPal.Models;

/// <summary>
/// Represents payment info model for PayPal (no card details required)
/// </summary>
public record PaymentInfoModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Payments.PayPal.PaymentInfo.Description")]
    public string Description { get; set; }
}

