using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Logging;
using Nop.Core.Http;
using Nop.Plugin.Payments.PayPal.Services;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayPal.Controllers;

/// <summary>
/// Represents public PayPal controller to handle return and cancel callbacks
/// </summary>
[AutoValidateAntiforgeryToken]
public class PayPalController : BasePublicController
{
    #region Fields

    private readonly ILogger _logger;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderService _orderService;
    private readonly PayPalHttpClient _payPalHttpClient;

    #endregion

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="PayPalController"/> with required services.
    /// </summary>
    /// <param name="logger">Logger for controller diagnostics.</param>
    /// <param name="orderProcessingService">Service used to update order state (e.g., mark as paid).</param>
    /// <param name="orderService">Service used to retrieve orders.</param>
    /// <param name="payPalHttpClient">HTTP client for interacting with the PayPal API.</param>
    public PayPalController(
        ILogger logger,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        PayPalHttpClient payPalHttpClient)
    {
        _logger = logger;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _payPalHttpClient = payPalHttpClient;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handles PayPal return callback for an order: validates the PayPal token, attempts to capture the PayPal order, marks the order as paid on success, and redirects the user to the appropriate checkout page.
    /// </summary>
    /// <param name="token">The PayPal order token returned by PayPal (expected to match the order's stored AuthorizationTransactionId).</param>
    /// <param name="orderId">The nopCommerce order identifier associated with the PayPal transaction.</param>
    /// <returns>A redirect result to either the checkout payment method selection, the checkout completed page for the order, or the site homepage depending on validation and processing outcomes.</returns>
    public async Task<IActionResult> Return(string token, int orderId)
    {
        if (string.IsNullOrEmpty(token))
        {
            await _logger.InsertLogAsync(LogLevel.Warning,
                $"PayPal return for order #{orderId} without token",
                "Received empty or missing PayPal token on return callback.");

            //do not treat checkout as successful; send the customer back to payment method selection
            return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_PAYMENT_METHOD);
        }

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        //ensure that the PayPal order identifier matches the one stored on the nopCommerce order
        var payPalOrderId = order.AuthorizationTransactionId;
        if (string.IsNullOrEmpty(payPalOrderId))
        {
            await _logger.InsertLogAsync(LogLevel.Warning,
                $"PayPal return for order #{order.Id} without stored PayPal order id",
                $"Received token '{token}' but order has no AuthorizationTransactionId.");

            return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = order.Id });
        }

        if (!string.Equals(payPalOrderId, token, StringComparison.InvariantCultureIgnoreCase))
        {
            await _logger.InsertLogAsync(LogLevel.Warning,
                $"PayPal return token mismatch for order #{order.Id}",
                $"Received token '{token}' but stored PayPal order id is '{payPalOrderId}'.");

            //do not attempt to capture a mismatched token; continue using the stored id
        }

        try
        {
            var captured = await _payPalHttpClient.CaptureOrderAsync(payPalOrderId);
            if (!captured)
            {
                await _logger.InsertLogAsync(LogLevel.Error,
                    $"Error capturing PayPal order for nop order #{order.Id}",
                    $"Capture failed for PayPal order {token}");

                return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = order.Id });
            }

            //mark order as paid
            await _orderProcessingService.MarkOrderAsPaidAsync(order);
        }
        catch (Exception exception)
        {
            await _logger.InsertLogAsync(LogLevel.Error,
                $"Error while processing PayPal return for order #{orderId}",
                exception.ToString());
        }

        return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = order.Id });
    }

    /// <summary>
    /// Handle a PayPal cancellation callback for an order and redirect the customer to the appropriate page.
    /// </summary>
    /// <param name="orderId">The identifier of the order associated with the cancellation.</param>
    /// <returns>A redirect to the payment method selection when the order exists; otherwise a redirect to the site's homepage.</returns>
    public async Task<IActionResult> Cancel(int orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_PAYMENT_METHOD);
    }

    #endregion
}

