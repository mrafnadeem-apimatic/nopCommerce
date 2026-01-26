using Nop.Core.Http;

namespace Nop.Plugin.Payments.PayPal;

/// <summary>
/// Represents PayPal payment plugin constants
/// </summary>
public static class PayPalDefaults
{
    /// <summary>
    /// Plugin system name
    /// </summary>
    public const string SystemName = "Payments.PayPal";

    /// <summary>
    /// Sandbox API base URL
    /// </summary>
    public const string SandboxApiBaseUrl = "https://api-m.sandbox.paypal.com/";

    /// <summary>
    /// Live API base URL
    /// </summary>
    public const string LiveApiBaseUrl = "https://api-m.paypal.com/";

    /// <summary>
    /// Name of the generic HTTP client used by this plugin
    /// </summary>
    public static string HttpClientName => NopHttpDefaults.DefaultHttpClient;
}


