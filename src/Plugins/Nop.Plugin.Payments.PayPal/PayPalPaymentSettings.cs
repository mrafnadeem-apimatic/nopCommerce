using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PayPal;

/// <summary>
/// Represents PayPal payment settings
/// </summary>
public class PayPalPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets PayPal client identifier
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets PayPal client secret
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use PayPal sandbox environment
    /// </summary>
    public bool UseSandbox { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the additional fee is specified as percentage.
    /// true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}


