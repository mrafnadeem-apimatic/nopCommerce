using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Plugin.Payments.PayPal.Services;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.PayPal.Controllers;

/// <summary>
/// Public controller used as a PayPal return/cancel callback endpoint.
/// </summary>
public class PayPalController : BasePluginController
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderProcessingService _orderProcessingService;
    protected readonly IOrderService _orderService;
    protected readonly PayPalOrdersService _payPalOrdersService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public PayPalController(ILocalizationService localizationService,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        PayPalOrdersService payPalOrdersService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _payPalOrdersService = payPalOrdersService;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Buyer approved the order on PayPal side.
    /// PayPal redirects back with a token (PayPal order id) and our order GUID.
    /// </summary>
    [HttpGet]
    public virtual async Task<IActionResult> Return(string token, string orderGuid)
    {
        if (!Guid.TryParse(orderGuid, out var guid))
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        var order = await _orderService.GetOrderByGuidAsync(guid);
        if (order == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        if (string.IsNullOrEmpty(token))
            return await HandleErrorAsync(order, await _localizationService.GetResourceAsync("Plugins.Payments.PayPal.Errors.MissingToken"));

        //verify that the callback token (PayPal order id) matches what we stored earlier
        if (string.IsNullOrEmpty(order.AuthorizationTransactionId) ||
            !string.Equals(order.AuthorizationTransactionId, token, StringComparison.Ordinal))
        {
            var mismatchMessage = await _localizationService.GetResourceAsync("Plugins.Payments.PayPal.Errors.OrderMismatch");
            return await HandleErrorAsync(order, mismatchMessage);
        }

        var captureResult = await _payPalOrdersService.CaptureOrderAsync(token);
        if (!captureResult.Success)
            return await HandleErrorAsync(order, captureResult.Error ?? "Failed to capture PayPal order.");

        //store capture-related transaction identifiers
        if (!string.IsNullOrEmpty(captureResult.CaptureId))
            order.CaptureTransactionId = captureResult.CaptureId;
        order.CaptureTransactionResult = captureResult.Status;
        await _orderService.UpdateOrderAsync(order);

        if (_orderProcessingService.CanMarkOrderAsPaid(order))
            await _orderProcessingService.MarkOrderAsPaidAsync(order);
        else
        {
            //capture succeeded on PayPal side, but nopCommerce order cannot be marked as paid
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                DisplayToCustomer = false,
                Note = $"PayPal capture succeeded but order could not be marked as paid. Current payment status: {order.PaymentStatus}",
                CreatedOnUtc = DateTime.UtcNow
            });
        }

        return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = order.Id });
    }

    /// <summary>
    /// Buyer cancelled payment on PayPal side.
    /// </summary>
    [HttpGet]
    public virtual async Task<IActionResult> Cancel(string orderGuid)
    {
        if (!Guid.TryParse(orderGuid, out var guid))
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        var order = await _orderService.GetOrderByGuidAsync(guid);
        if (order == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        //cancel order if possible
        if (_orderProcessingService.CanCancelOrder(order))
            await _orderProcessingService.CancelOrderAsync(order, false);

        return RedirectToRoute(NopRouteNames.Standard.ORDER_DETAILS, new { orderId = order.Id });
    }

    protected virtual async Task<IActionResult> HandleErrorAsync(Order order, string message)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            DisplayToCustomer = false,
            Note = $"PayPal error: {message}",
            CreatedOnUtc = DateTime.UtcNow
        });

        //do not change order status here; keep it pending so that customer/admin can retry
        return RedirectToRoute(NopRouteNames.Standard.ORDER_DETAILS, new { orderId = order.Id });
    }

    #endregion
}

