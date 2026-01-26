using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayPal.Models;

public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPal.Fields.ClientId")]
    public string ClientId { get; set; }
    public bool ClientId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPal.Fields.ClientSecret")]
    public string ClientSecret { get; set; }
    public bool ClientSecret_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPal.Fields.UseSandbox")]
    public bool UseSandbox { get; set; }
    public bool UseSandbox_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPal.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPal.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }
}


