using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PayPal;

/// <summary>
/// Represents settings of PayPal payment plugin
/// </summary>
public class PayPalPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets the PayPal REST API client identifier
    /// NOTE: Hardcoded here temporarily for testing; replace with your real client ID.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the PayPal REST API client secret
    /// NOTE: Hardcoded here temporarily for testing; replace with your real client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the PayPal Sandbox environment
    /// </summary>
    public bool UseSandbox { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the additional fee is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}

