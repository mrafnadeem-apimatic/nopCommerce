using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPal.Infrastructure;

/// <summary>
/// Represents plugin route provider for PayPal payment plugin.
/// </summary>
public class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // public callbacks (use PaymentPayPal segment to match configured PayPal return URLs)
        endpointRouteBuilder.MapControllerRoute(name: "Plugin.Payments.PayPal.Return",
            pattern: "Plugins/PaymentPayPal/Return",
            defaults: new { controller = "PayPal", action = "Return" });

        endpointRouteBuilder.MapControllerRoute(name: "Plugin.Payments.PayPal.Cancel",
            pattern: "Plugins/PaymentPayPal/Cancel",
            defaults: new { controller = "PayPal", action = "Cancel" });
    }

    public int Priority => 0;
}

