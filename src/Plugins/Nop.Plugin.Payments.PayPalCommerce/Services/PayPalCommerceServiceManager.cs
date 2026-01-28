using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Utilities;
using SdkModels = PaypalServerSdk.Standard.Models;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Tax;
using Nop.Core.Http;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Authentication;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Identity;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.Enums;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Onboarding;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Orders;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Payments;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.PaymentTokens;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Webhooks;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Pickup;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Framework.Mvc.Routing;
using Address = Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.Address;
using Environment = System.Environment;
using NopAddress = Nop.Core.Domain.Common.Address;
using NopOrder = Nop.Core.Domain.Orders.Order;
using NopShippingOption = Nop.Core.Domain.Shipping.ShippingOption;
using Order = Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.Order;
using PaymentType = Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType;
using ShippingOption = Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.ShippingOption;
using Microsoft.Extensions.Logging;

namespace Nop.Plugin.Payments.PayPalCommerce.Services;

/// <summary>
/// Represents the plugin service manager
/// </summary>
public class PayPalCommerceServiceManager
{
    #region Fields

    private readonly CurrencySettings _currencySettings;
    private readonly CustomerSettings _customerSettings;
    private readonly IAddressService _addressService;
    private readonly IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> _checkoutAttributeParser;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILocalizationService _localizationService;
    private readonly Nop.Services.Logging.ILogger _logger;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderService _orderService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IPaymentService _paymentService;
    private readonly IPickupPluginManager _pickupPluginManager;
    private readonly IPictureService _pictureService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IProductService _productService;
    private readonly IShipmentService _shipmentService;
    private readonly IShippingService _shippingService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IShortTermCacheManager _shortTermCacheManager;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly ITaxService _taxService;
    private readonly IWebHelper _webHelper;
    private readonly IWorkContext _workContext;
    private readonly OrderSettings _orderSettings;
    private readonly PayPalCommerceHttpClient _httpClient;
    private readonly PayPalTokenService _tokenService;
    private readonly ShippingSettings _shippingSettings;
    private readonly TaxSettings _taxSettings;
    private readonly PaypalServerSdkClient _paypalServerSdkClient;

    #endregion

    #region Ctor

    public PayPalCommerceServiceManager(CurrencySettings currencySettings,
        CustomerSettings customerSettings,
        IAddressService addressService,
        IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        Nop.Services.Logging.ILogger logger,
        INopUrlHelper nopUrlHelper,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IPickupPluginManager pickupPluginManager,
        IPictureService pictureService,
        IPriceCalculationService priceCalculationService,
        IProductService productService,
        IShipmentService shipmentService,
        IShippingService shippingService,
        IShoppingCartService shoppingCartService,
        IShortTermCacheManager shortTermCacheManager,
        IStateProvinceService stateProvinceService,
        IStoreContext storeContext,
        IStoreService storeService,
        ITaxService taxService,
        IWebHelper webHelper,
        IWorkContext workContext,
        OrderSettings orderSettings,
        PayPalCommerceHttpClient httpClient,
        PayPalTokenService tokenService,
        ShippingSettings shippingSettings,
        TaxSettings taxSettings,
        PaypalServerSdkClient paypalServerSdkClient)
    {
        _currencySettings = currencySettings;
        _customerSettings = customerSettings;
        _addressService = addressService;
        _checkoutAttributeParser = checkoutAttributeParser;
        _countryService = countryService;
        _currencyService = currencyService;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _logger = logger;
        _nopUrlHelper = nopUrlHelper;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _paymentPluginManager = paymentPluginManager;
        _paymentService = paymentService;
        _pickupPluginManager = pickupPluginManager;
        _pictureService = pictureService;
        _priceCalculationService = priceCalculationService;
        _productService = productService;
        _shipmentService = shipmentService;
        _shippingService = shippingService;
        _shoppingCartService = shoppingCartService;
        _shortTermCacheManager = shortTermCacheManager;
        _stateProvinceService = stateProvinceService;
        _storeContext = storeContext;
        _storeService = storeService;
        _taxService = taxService;
        _webHelper = webHelper;
        _workContext = workContext;
        _orderSettings = orderSettings;
        _httpClient = httpClient;
        _tokenService = tokenService;
        _shippingSettings = shippingSettings;
        _taxSettings = taxSettings;
        _paypalServerSdkClient = paypalServerSdkClient;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Handle function and get result
    /// </summary>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="function">Function</param>
    /// <param name="logErrors">Whether to log errors</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result; error message if exists
    /// </returns>
    private async Task<(TResult Result, string Error)> HandleFunctionAsync<TResult>(Func<Task<TResult>> function, bool logErrors = true)
    {
        try
        {
            //invoke function
            return (await function(), default);
        }
        catch (Exception exception)
        {
            //log errors
            if (logErrors)
            {
                var logMessage = $"{PayPalCommerceDefaults.SystemName} error:{Environment.NewLine}{exception.Message}";
                var exceptionToLog = exception is NopException nopException ? nopException.InnerException ?? nopException : exception;
                var customer = await _workContext.GetCurrentCustomerAsync();
                await _logger.ErrorAsync(logMessage, exceptionToLog, customer);
            }

            return (default, exception.Message);
        }
    }

    #region Components

    /// <summary>
    /// Prepare amount value for Pay Later messages
    /// </summary>
    /// <param name="placement">Button placement</param>
    /// <param name="customer">Customer</param>
    /// <param name="currencyCode">Currency code</param>
    /// <param name="productId">Product id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the amount value
    /// </returns>
    private async Task<string> PrepareMessagesAmountAsync(ButtonPlacement placement, Customer customer, string currencyCode, int? productId)
    {
        //cache result during HTTP request
        return await _shortTermCacheManager.GetAsync(async () =>
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var product = await _productService.GetProductByIdAsync(productId ?? 0);
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

            var (_, _, _, subTotal, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, true);

            var amount = placement switch
            {
                ButtonPlacement.Cart => subTotal,
                ButtonPlacement.Product when product is not null
                    => (await _priceCalculationService.GetFinalPriceAsync(product, customer, store)).finalPrice, //+ subTotal,
                ButtonPlacement.PaymentMethod
                    => (await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, null, usePaymentMethodAdditionalFee: false))
                        .shoppingCartTotal ?? subTotal,
                _ => (decimal?)null
            };

            return amount is not null ? PrepareMoney(amount.Value, currencyCode).Value : null;
        }, new($"{PayPalCommerceDefaults.SystemName}.Messages.Amount-{productId ?? 0}"));
    }

    #endregion

    #region Orders

    /// <summary>
    /// Prepare money object
    /// </summary>
    /// <param name="value">Amount value</param>
    /// <param name="currencyCode">Currency code</param>
    /// <returns>Money object</returns>
    private static Money PrepareMoney(decimal value, string currencyCode)
    {
        var format = PayPalCommerceDefaults.CurrenciesWithoutDecimals.Contains(currencyCode.ToUpper()) ? "0" : "0.00";
        return new()
        {
            CurrencyCode = currencyCode,
            Value = value.ToString(format, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Prepare server SDK money object
    /// </summary>
    /// <param name="value">Amount value</param>
    /// <param name="currencyCode">Currency code</param>
    /// <returns>Server SDK money object</returns>
    private static SdkModels.Money PrepareSdkMoney(decimal value, string currencyCode)
    {
        var format = PayPalCommerceDefaults.CurrenciesWithoutDecimals.Contains(currencyCode.ToUpper()) ? "0" : "0.00";
        return new SdkModels.Money
        {
            CurrencyCode = currencyCode,
            MValue = value.ToString(format, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Convert money object to decimal value
    /// </summary>
    /// <param name="value">Amount value</param>
    /// <returns>Decimal value</returns>
    private static decimal ConvertMoney(Money amount)
    {
        return decimal.TryParse(amount?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : decimal.Zero;
    }

    /// <summary>
    /// Prepare shopping cart details
    /// </summary>
    /// <param name="placement">Button placement</param>
    /// <param name="withShipping">Whether to prepare shipping details</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the cart details
    /// </returns>
    private async Task<CartDetails> PrepareCartDetailsAsync(ButtonPlacement placement, bool withShipping = true)
    {
        //get the primary store currency
        var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
        if (string.IsNullOrEmpty(currencyCode))
            throw new NopException("Primary store currency not set");

        //get customer shopping cart
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        if (!cart.Any())
            throw new NopException("Shopping cart is empty");

        //get billing address
        var billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId ?? 0);
        if (placement == ButtonPlacement.PaymentMethod && billingAddress is null)
            throw new NopException("Customer billing address not set");

        var shippingIsRequired = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);

        var details = new CartDetails
        {
            Placement = placement,
            CurrencyCode = currencyCode,
            Customer = customer,
            Store = store,
            Cart = cart.ToList(),
            BillingAddress = billingAddress,
            ShippingIsRequired = shippingIsRequired,
        };

        if (!withShipping)
            return details;

        //get shipping details
        details.ShippingOption = await _genericAttributeService
            .GetAttributeAsync<NopShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);
        details.PickupPoint = await _genericAttributeService
            .GetAttributeAsync<PickupPoint>(customer, NopCustomerDefaults.SelectedPickupPointAttribute, store.Id);
        details.IsPickup = _shippingSettings.AllowPickupInStore && details.PickupPoint is not null;
        details.ShippingAddress = details.IsPickup ? new NopAddress
        {
            Address1 = details.PickupPoint.Address,
            City = details.PickupPoint.City,
            County = details.PickupPoint.County,
            CountryId = (await _countryService.GetCountryByTwoLetterIsoCodeAsync(details.PickupPoint.CountryCode))?.Id,
            StateProvinceId = (await _stateProvinceService.GetStateProvinceByAbbreviationAsync(details.PickupPoint.StateAbbreviation,
                (await _countryService.GetCountryByTwoLetterIsoCodeAsync(details.PickupPoint.CountryCode))?.Id))?.Id,
            ZipPostalCode = details.PickupPoint.ZipPostalCode,
            CreatedOnUtc = DateTime.UtcNow
        } : await _addressService.GetAddressByIdAsync(customer.ShippingAddressId ?? 0);
        if (placement == ButtonPlacement.PaymentMethod && shippingIsRequired && details.ShippingAddress is null)
            throw new NopException("Customer shipping address not set");

        return details;
    }

    /// <summary>
    /// Prepare order context
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="details">Shopping cart details</param>
    /// <param name="orderGuid">Order internal id</param>
    /// <param name="isApplePay">Apple Pay payment</param>
    /// <returns>Experience context</returns>
    private ExperienceContext PrepareOrderContext(PayPalCommerceSettings settings, CartDetails details, string orderGuid, bool isApplePay = false)
    {
        var protocol = _webHelper.GetCurrentRequestProtocol();

        var shippingPreference = ShippingPreferenceType.NO_SHIPPING.ToString().ToUpper();
        if (details.ShippingIsRequired)
        {
            shippingPreference = details.Placement == ButtonPlacement.PaymentMethod && !isApplePay
                ? ShippingPreferenceType.SET_PROVIDED_ADDRESS.ToString().ToUpper()
                : ShippingPreferenceType.GET_FROM_FILE.ToString().ToUpper();
        }

        return new()
        {
            //Locale = null, //PayPal auto detects this
            BrandName = CommonHelper.EnsureMaximumLength(details.Store.Name, 127),
            LandingPage = LandingPageType.NO_PREFERENCE.ToString().ToUpper(),
            UserAction = details.Placement == ButtonPlacement.PaymentMethod && settings.SkipOrderConfirmPage
                ? UserActionType.PAY_NOW.ToString().ToUpper()
                : UserActionType.CONTINUE.ToString().ToUpper(),
            CancelUrl = details.Placement switch
            {
                ButtonPlacement.PaymentMethod => _nopUrlHelper.RouteUrl(PayPalCommerceDefaults.Route.PaymentInfo, null, protocol),
                ButtonPlacement.Cart or ButtonPlacement.Product => _nopUrlHelper.RouteUrl(NopRouteNames.General.CART, null, protocol),
                _ => null
            },
            ReturnUrl = _nopUrlHelper.RouteUrl(PayPalCommerceDefaults.Route.ConfirmOrder, new { token = orderGuid, approve = true }, protocol),
            PaymentMethodPreference = settings.ImmediatePaymentRequired
                ? PaymentMethodPreferenceType.IMMEDIATE_PAYMENT_REQUIRED.ToString().ToUpper()
                : PaymentMethodPreferenceType.UNRESTRICTED.ToString().ToUpper(),
            ShippingPreference = shippingPreference
        };
    }

    /// <summary>
    /// Prepare recurring payment context
    /// </summary>
    /// <param name="details">Shopping cart details</param>
    /// <returns>Experience context</returns>
    private ExperienceContext PrepareRecurringPaymentContext(CartDetails details)
    {
        var protocol = _webHelper.GetCurrentRequestProtocol();

        return new()
        {
            BrandName = CommonHelper.EnsureMaximumLength(details.Store.Name, 127),
            CancelUrl = _nopUrlHelper.RouteUrl(PayPalCommerceDefaults.Route.PaymentInfo, null, protocol),
            ReturnUrl = _nopUrlHelper.RouteUrl(PayPalCommerceDefaults.Route.ApproveToken, null, protocol),
            PaymentMethodPreference = PaymentMethodPreferenceType.IMMEDIATE_PAYMENT_REQUIRED.ToString().ToUpper(),
            ShippingPreference = details.ShippingIsRequired
                ? ShippingPreferenceType.SET_PROVIDED_ADDRESS.ToString().ToUpper()
                : ShippingPreferenceType.NO_SHIPPING.ToString().ToUpper()
        };
    }

    /// <summary>
    /// Prepare order billing details
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="details">Shopping cart details</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the payer with the billing details
    /// </returns>
    private async Task<Payer> PrepareBillingDetailsAsync(PayPalCommerceSettings settings, CartDetails details)
    {
        var customer = details.Customer;
        var address = details.BillingAddress;
        var isPaymentMethodPage = details.Placement == ButtonPlacement.PaymentMethod;

        var email = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.Email : customer.Email, 254);
        var name = new Name
        {
            GivenName = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.FirstName : customer.FirstName, 140),
            Surname = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.LastName : customer.LastName, 140)
        };
        //phone number format is unpredictable
        //var phone = isPaymentMethodPage
        //    ? (!string.IsNullOrEmpty(address.PhoneNumber) ? new Phone { PhoneNumber = new() { NationalNumber = CommonHelper.EnsureMaximumLength(CommonHelper.EnsureNumericOnly(address.PhoneNumber), 14) } } : null)
        //    : !string.IsNullOrEmpty(customer.Phone) ? new Phone { PhoneNumber = new() { NationalNumber = CommonHelper.EnsureMaximumLength(CommonHelper.EnsureNumericOnly(customer.Phone), 14) } } : null;
        var birthDate = customer.DateOfBirth?.ToString("yyyy-MM-dd");
        var country = await _countryService.GetCountryByIdAsync(isPaymentMethodPage ? address.CountryId ?? 0 : customer.CountryId);
        var state = await _stateProvinceService
            .GetStateProvinceByIdAsync(isPaymentMethodPage ? address.StateProvinceId ?? 0 : customer.StateProvinceId);
        var billingAddress = new Address
        {
            AddressLine1 = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.Address1 : customer.StreetAddress, 300),
            AddressLine2 = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.Address2 : customer.StreetAddress2, 300),
            AdminArea2 = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.City : customer.City, 120),
            AdminArea1 = CommonHelper.EnsureMaximumLength(state?.Abbreviation, 300),
            CountryCode = CommonHelper.EnsureMaximumLength(country?.TwoLetterIsoCode, 2),
            PostalCode = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.ZipPostalCode : customer.ZipPostalCode, 60)
        };

        //country is required
        if (string.IsNullOrEmpty(billingAddress.CountryCode))
            billingAddress = null;

        //try to get Vault customer id
        var id = settings.UseVault ? await GetVaultCustomerIdAsync(settings, customer.Id) : null;

        return new()
        {
            Id = id,
            EmailAddress = email,
            Name = name,
            BirthDate = birthDate,
            Address = billingAddress,
            MerchantCustomerId = string.IsNullOrEmpty(id) ? customer.Id.ToString() : null //we can only use a single identifier
        };
    }

    /// <summary>
    /// Prepare order items
    /// </summary>
    /// <param name="details">Shopping cart details</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of purchase items
    /// </returns>
    private async Task<List<Item>> PrepareOrderItemsAsync(CartDetails details)
    {
        //cart items
        var items = await details.Cart.SelectAwait(async item =>
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product is null)
                return null;

            var sku = await _productService.FormatSkuAsync(product, item.AttributesXml);
            var url = await _nopUrlHelper.RouteGenericUrlAsync(product, _webHelper.GetCurrentRequestProtocol());

            var picture = await _pictureService.GetProductPictureAsync(product, item.AttributesXml);
            //PayPal doesn't currently support WebP images
            var ext = await _pictureService.GetFileExtensionFromMimeTypeAsync(picture?.MimeType);
            var (imageUrl, _) = ext != "webp" ? await _pictureService.GetPictureUrlAsync(picture) : default;

            var (itemSubTotal, itemDiscount, _, _) = await _shoppingCartService.GetSubTotalAsync(item, true);
            var unitPrice = itemSubTotal / item.Quantity;
            var (unitPriceExclTax, _) = await _taxService.GetProductPriceAsync(product, unitPrice, false, details.Customer);

            return new Item
            {
                Name = CommonHelper.EnsureMaximumLength(product.Name, 127),
                Description = CommonHelper.EnsureMaximumLength(product.ShortDescription, 127),
                Sku = CommonHelper.EnsureMaximumLength(sku, 127),
                Quantity = item.Quantity.ToString(),
                Category = product.IsDownload
                    ? CategoryType.DIGITAL_GOODS.ToString().ToUpper()
                    : CategoryType.PHYSICAL_GOODS.ToString().ToUpper(),
                Url = url,
                ImageUrl = imageUrl,
                UnitAmount = PrepareMoney(unitPriceExclTax, details.CurrencyCode)
            };
        }).Where(item => item is not null).ToListAsync();

        //and checkout attributes
        var checkoutAttributes = await _genericAttributeService
            .GetAttributeAsync<string>(details.Customer, NopCustomerDefaults.CheckoutAttributes, details.Store.Id);
        var checkoutAttributeValues = _checkoutAttributeParser.ParseAttributeValues(checkoutAttributes);
        await foreach (var (attribute, values) in checkoutAttributeValues)
        {
            await foreach (var attributeValue in values)
            {
                var (attributePriceExclTax, _) = await _taxService
                    .GetCheckoutAttributePriceAsync(attribute, attributeValue, false, details.Customer);

                items.Add(new()
                {
                    Name = CommonHelper.EnsureMaximumLength(attribute.Name, 127),
                    Description = CommonHelper.EnsureMaximumLength($"{attribute.Name} - {attributeValue.Name}", 127),
                    Quantity = 1.ToString(),
                    UnitAmount = PrepareMoney(attributePriceExclTax, details.CurrencyCode)
                });
            }
        }

        return items;
    }

    /// <summary>
    /// Prepare order amount with breakdown
    /// </summary>
    /// <param name="details">Shopping cart details</param>
    /// <param name="items">Purchase items</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order amount with breakdown
    /// </returns>
    private async Task<OrderMoney> PrepareOrderMoneyAsync(CartDetails details, List<Item> items)
    {
        //in some rare cases we need an additional item to adjust the order total
        //this can happen due to complex discounts or a large order and related to rounding in calculations
        //PayPal uses two decimal places, while nopCommerce can use more complex types of rounding (configured for each currency separately) 
        var adjustmentName = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Adjustment.Name");
        var adjustmentDescription = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Adjustment.Description");
        if (items.FirstOrDefault(item => adjustmentName.Equals(item.Name) && adjustmentDescription.Equals(item.Description)) is Item adjustmentItem)
            items.Remove(adjustmentItem);

        var (total, _, _, _, _, _) = await _orderTotalCalculationService
            .GetShoppingCartTotalAsync(details.Cart, usePaymentMethodAdditionalFee: false);
        if (total is null)
        {
            if (details.Placement == ButtonPlacement.PaymentMethod)
                throw new NopException("Shopping cart total couldn't be calculated now");

            //on product and cart pages the total is not yet calculated, so use subtotal here
            var (_, _, subTotal, _, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(details.Cart, includingTax: false);
            total = subTotal;
        }
        var orderTotal = PrepareMoney(total.Value, details.CurrencyCode);

        var (shippingTotal, _, _) = await _orderTotalCalculationService
            .GetShoppingCartShippingTotalAsync(details.Cart, includingTax: _taxSettings.ShippingPriceIncludesTax);
        var orderShippingTotal = PrepareMoney(shippingTotal ?? decimal.Zero, details.CurrencyCode);

        var (taxTotal, _) = await _orderTotalCalculationService.GetTaxTotalAsync(details.Cart, usePaymentMethodAdditionalFee: false);
        var orderTaxTotal = PrepareMoney(taxTotal, details.CurrencyCode);

        var itemAdjustment = decimal.Zero;
        var itemTotal = items.Sum(item => ConvertMoney(item.UnitAmount) * int.Parse(item.Quantity));
        var discountTotal = itemTotal + ConvertMoney(orderTaxTotal) + ConvertMoney(orderShippingTotal) - ConvertMoney(orderTotal);
        if (discountTotal < decimal.Zero)
        {
            itemAdjustment = -discountTotal;
            itemTotal += itemAdjustment;
            discountTotal = decimal.Zero;
        }
        var orderItemTotal = PrepareMoney(itemTotal, details.CurrencyCode);
        var orderDiscount = PrepareMoney(discountTotal, details.CurrencyCode);

        //set adjustment item if needed
        if (itemAdjustment > decimal.Zero)
        {
            var unitAmount = PrepareMoney(itemAdjustment, details.CurrencyCode);
            if (ConvertMoney(unitAmount) > decimal.Zero)
            {
                items.Add(new()
                {
                    Name = adjustmentName,
                    Description = adjustmentDescription,
                    Quantity = 1.ToString(),
                    UnitAmount = unitAmount
                });
            }
        }

        return new()
        {
            CurrencyCode = details.CurrencyCode,
            Value = orderTotal.Value,
            Breakdown = new()
            {
                ItemTotal = orderItemTotal,
                TaxTotal = orderTaxTotal,
                Shipping = orderShippingTotal,
                Discount = orderDiscount
            }
        };
    }

    /// <summary>
    /// Prepare order shipping details
    /// </summary>
    /// <param name="details">Shopping cart details</param>
    /// <param name="selectedOptionId">Selected shipping option</param>
    /// <param name="isApplePay">Apple Pay payment</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping details
    /// </returns>
    private async Task<Shipping> PrepareShippingDetailsAsync(CartDetails details, string selectedOptionId, bool isApplePay = false)
    {
        if (!details.ShippingIsRequired)
            return null;

        var shippingAddress = details.ShippingAddress;
        var fullName = shippingAddress is not null && !details.IsPickup
            ? await _customerService.GetCustomerFullNameAsync(new() { FirstName = shippingAddress.FirstName, LastName = shippingAddress.LastName })
            : null;
        if (string.IsNullOrEmpty(fullName))
            fullName = await _customerService.GetCustomerFullNameAsync(details.Customer);

        //if the shipping option type is set to PICKUP, then the full name should start with S2S meaning ship to store (for example, S2S My Store)
        if (details.IsPickup && details.PickupPoint is not null)
            fullName = $"S2S {details.PickupPoint.Name}";

        var address = shippingAddress is not null ? new Address
        {
            AddressLine1 = CommonHelper.EnsureMaximumLength(shippingAddress.Address1, 300),
            AddressLine2 = CommonHelper.EnsureMaximumLength(shippingAddress.Address2, 300),
            AdminArea2 = CommonHelper.EnsureMaximumLength(shippingAddress.City, 120),
            AdminArea1 = CommonHelper.EnsureMaximumLength((await _stateProvinceService
                .GetStateProvinceByAddressAsync(shippingAddress))?.Abbreviation, 300),
            CountryCode = (await _countryService.GetCountryByIdAsync(shippingAddress.CountryId ?? 0))?.TwoLetterIsoCode,
            PostalCode = CommonHelper.EnsureMaximumLength(shippingAddress.ZipPostalCode, 60)
        } : null;

        var shipping = new Shipping
        {
            Name = new() { FullName = CommonHelper.EnsureMaximumLength(fullName, 300), },
            Address = address
        };

        if (details.Placement == ButtonPlacement.PaymentMethod && !isApplePay)
        {
            shipping.Type = details.IsPickup
                ? ShippingType.SHIPPING.ToString().ToUpper() //PICKUP_IN_STORE option doesn't work for some reason
                : ShippingType.SHIPPING.ToString().ToUpper();

            return shipping;
        }

        var (shippingOptions, pickupPoints) = await PrepareShippingOptionsAsync(details);
        if (!shippingOptions?.Any() ?? true)
            throw new NopException("No available shipping options");

        var selectedShippingOption = shippingOptions.FirstOrDefault();
        if (!string.IsNullOrEmpty(selectedOptionId))
        {
            var existingOption = shippingOptions
                .FirstOrDefault(option => string.Equals(option.Name, selectedOptionId, StringComparison.InvariantCultureIgnoreCase))
                ?? throw new NopException("Selected shipping option is unavailable");

            selectedShippingOption = existingOption;
        }

        if (selectedShippingOption is null)
            throw new NopException("Selected shipping option is unavailable");

        PickupPoint pickupPoint = null;
        if (selectedShippingOption.IsPickupInStore)
        {
            pickupPoint = await pickupPoints.FirstOrDefaultAwaitAsync(async point =>
                string.Equals(await GetShippingOptionNameAsync(new() { Name = point.Name, IsPickupInStore = true }), selectedShippingOption.Name) &&
                string.Equals(point.ProviderSystemName, selectedShippingOption.ShippingRateComputationMethodSystemName));

            details.IsPickup = true;
            details.PickupPoint = pickupPoint;

            //if the shipping option type is set to PICKUP, then the full name should start with S2S meaning ship to store (for example, S2S My Store)
            if (details.IsPickup && details.PickupPoint is not null)
                shipping.Name.FullName = $"S2S {details.PickupPoint.Name}";
        }
        details.ShippingOption = selectedShippingOption;

        //save selected options in attributes
        await _genericAttributeService
            .SaveAttributeAsync(details.Customer, NopCustomerDefaults.SelectedShippingOptionAttribute, selectedShippingOption, details.Store.Id);
        await _genericAttributeService
            .SaveAttributeAsync(details.Customer, NopCustomerDefaults.SelectedPickupPointAttribute, pickupPoint, details.Store.Id);

        async Task<ShippingOption> convertOptionAsync(NopShippingOption option)
        {
            var (adjustedShippingRate, _) = await _orderTotalCalculationService
                .AdjustShippingRateAsync(option.Rate, details.Cart, option.IsPickupInStore);
            //var (rate, _) = await _taxService.GetShippingPriceAsync(adjustedShippingRate, details.Customer);
            //PayPal currently handles taxable shipping incorrectly, so we display shipping rates without tax, but it'll be included to tax total
            var rate = adjustedShippingRate;

            return new ShippingOption
            {
                Id = CommonHelper.EnsureMaximumLength(option.Name, 127),
                Label = CommonHelper.EnsureMaximumLength(option.Name, 127),
                Selected = false,
                Type = option.IsPickupInStore
                    ? ShippingType.PICKUP.ToString().ToUpper()
                    : ShippingType.SHIPPING.ToString().ToUpper(),
                Amount = PrepareMoney(rate, details.CurrencyCode)
            };
        }

        shipping.Options = details.Placement == ButtonPlacement.PaymentMethod
            ? [await convertOptionAsync(selectedShippingOption)]
            : await shippingOptions.SelectAwait(async option => await convertOptionAsync(option)).ToListAsync();

        //set default shipping option
        (shipping.Options
            .FirstOrDefault(option => string.Equals(option.Id, details.ShippingOption?.Name, StringComparison.InvariantCultureIgnoreCase))
            ?? shipping.Options.First())
            .Selected = true;

        return shipping;
    }

    /// <summary>
    /// Prepare available shipping options
    /// </summary>
    /// <param name="details">Shopping cart details</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of shipping options; list of pickup points
    /// </returns>
    private async Task<(List<NopShippingOption> ShippingOptions, List<PickupPoint> PickupPoints)> PrepareShippingOptionsAsync(CartDetails details)
    {
        if (!details.ShippingIsRequired)
            return (null, null);

        if (details.ShippingAddress is null && !_shippingSettings.AllowPickupInStore)
            return (null, null);

        var shippingOptions = new List<NopShippingOption>();
        var pickupPoints = new List<PickupPoint>();

        //pickup points
        if (_shippingSettings.AllowPickupInStore)
        {
            var pickupPointProviders = await _pickupPluginManager.LoadActivePluginsAsync(details.Customer, details.Store.Id);
            if (pickupPointProviders.Any())
            {
                var pickupPointsResponse = await _shippingService
                    .GetPickupPointsAsync(details.Cart, details.BillingAddress, details.Customer, storeId: details.Store.Id);
                if (pickupPointsResponse.Success)
                {
                    shippingOptions.AddRange(await pickupPointsResponse.PickupPoints.SelectAwait(async point => new NopShippingOption
                    {
                        Name = await GetShippingOptionNameAsync(new() { Name = point.Name, IsPickupInStore = true }),
                        Rate = point.PickupFee,
                        Description = point.Description,
                        ShippingRateComputationMethodSystemName = point.ProviderSystemName,
                        IsPickupInStore = true,
                        DisplayOrder = point.DisplayOrder,
                        TransitDays = point.TransitDays
                    }).ToListAsync());
                    pickupPoints.AddRange(pickupPointsResponse.PickupPoints);
                }
            }
        }

        //and shipping options
        if (details.ShippingAddress is not null)
        {
            var shippingOptionResponse = await _shippingService
                .GetShippingOptionsAsync(details.Cart, details.ShippingAddress, details.Customer, storeId: details.Store.Id);
            if (shippingOptionResponse.Success)
                shippingOptions.AddRange(shippingOptionResponse.ShippingOptions);
        }

        //sort options
        shippingOptions = (_shippingSettings.ShippingSorting switch
        {
            ShippingSortingEnum.ShippingCost => shippingOptions.OrderBy(option => option.Rate),
            _ => shippingOptions.OrderBy(option => option.DisplayOrder)
        }).ToList();

        return (shippingOptions, pickupPoints);
    }

    /// <summary>
    /// Prepare the updated shipping details
    /// </summary>
    /// <param name="details">Shopping cart details</param>
    /// <param name="email">Customer email</param>
    /// <param name="selectedAddress">Selected shipping address</param>
    /// <param name="selectedOption">Selected shipping option</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping details
    /// </returns>
    private async Task<Shipping> PrepareUpdatedShippingAsync(CartDetails details, string email,
        (string City, string State, string Country, string PostalCode) selectedAddress,
        (string Id, string Type) selectedOption)
    {
        //change shipping address when customer selects another one
        if (!string.IsNullOrEmpty(selectedAddress.City) && !string.IsNullOrEmpty(selectedAddress.State) &&
            !string.IsNullOrEmpty(selectedAddress.Country) && !string.IsNullOrEmpty(selectedAddress.PostalCode))
        {
            var country = await _countryService.GetCountryByTwoLetterIsoCodeAsync(selectedAddress.Country);
            var state = await _stateProvinceService.GetStateProvinceByAbbreviationAsync(selectedAddress.State, country?.Id);
            var newShippingAddress = await PrepareCustomerAddressAsync(details.Customer, new()
            {
                Email = email ?? details.Customer.Email,
                City = selectedAddress.City,
                StateProvinceId = state?.Id,
                CountryId = country?.Id,
                ZipPostalCode = selectedAddress.PostalCode
            });
            if (newShippingAddress.Id != details.Customer.ShippingAddressId)
            {
                details.Customer.ShippingAddressId = newShippingAddress.Id;
                await _customerService.UpdateCustomerAsync(details.Customer);
            }
        }

        //change shipping option when customer selects another one
        if (string.IsNullOrEmpty(selectedOption.Id))
        {
            var shippingOption = await _genericAttributeService
                .GetAttributeAsync<NopShippingOption>(details.Customer, NopCustomerDefaults.SelectedShippingOptionAttribute, details.Store.Id);
            if (shippingOption is not null)
            {
                var type = shippingOption.IsPickupInStore ? ShippingType.PICKUP.ToString() : ShippingType.SHIPPING.ToString();
                selectedOption = (shippingOption.Name, type);
            }
        }

        //set new parameters to update shipping details
        details.BillingAddress = await _customerService.GetCustomerBillingAddressAsync(details.Customer);
        details.ShippingAddress = await _customerService.GetCustomerShippingAddressAsync(details.Customer);
        details.IsPickup =
            selectedOption.Type?.ToUpper() == ShippingType.PICKUP.ToString() ||
            selectedOption.Type?.ToUpper() == ShippingType.PICKUP_IN_STORE.ToString() ||
            selectedOption.Type?.ToUpper() == ShippingType.PICKUP_FROM_PERSON.ToString();
        if (details.ShippingAddress is null && !details.IsPickup)
            return null;

        var shipping = await PrepareShippingDetailsAsync(details, selectedOption.Id);

        return shipping;
    }

    /// <summary>
    /// Prepare the purchase unit details
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="details">Shopping cart details</param>
    /// <param name="orderGuid">Order guid</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the purchase unit details
    /// </returns>
    private async Task<PurchaseUnit> PreparePurchaseUnitAsync(PayPalCommerceSettings settings, CartDetails details, string orderGuid)
    {
        var shipping = await PrepareShippingDetailsAsync(details, details.ShippingOption?.Name);
        var items = await PrepareOrderItemsAsync(details);
        var orderAmount = await PrepareOrderMoneyAsync(details, items);
        var cardData = new CardData
        {
            Level2 = new() { InvoiceId = CommonHelper.EnsureMaximumLength(orderGuid, 127) },
            Level3 = new()
            {
                LineItems = items,
                ShippingAmount = orderAmount.Breakdown.Shipping,
                ShippingAddress = shipping?.Address,
                ShipsFromPostalCode = CommonHelper.EnsureMaximumLength((await _addressService
                    .GetAddressByIdAsync(_shippingSettings.ShippingOriginAddressId))?.ZipPostalCode, 60)
            },
        };

        return new PurchaseUnit
        {
            CustomId = CommonHelper.EnsureMaximumLength(orderGuid, 127),
            InvoiceId = CommonHelper.EnsureMaximumLength(orderGuid, 127),
            Description = CommonHelper.EnsureMaximumLength($"Purchase at '{details.Store.Name}'", 127),
            SoftDescriptor = CommonHelper.EnsureMaximumLength(details.Store.Name, 22),
            Payee = new() { MerchantId = settings.MerchantId },
            Items = items,
            Amount = orderAmount,
            Shipping = shipping,
            SupplementaryData = new() { Card = cardData }
        };
    }

    /// <summary>
    /// Prepare patches to update an order
    /// </summary>
    /// <param name="purchaseUnit">Purchase unit details</param>
    /// <returns>List of patch objects</returns>
    private static List<Patch<object>> PreparePatches(PurchaseUnit purchaseUnit)
    {
        var patches = new List<Patch<object>>
        {
            new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/amount",
                Value = purchaseUnit.Amount
            },
            new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/items",
                Value = purchaseUnit.Items
            },
            new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/supplementary_data/card",
                Value = purchaseUnit.SupplementaryData.Card
            }
        };

        if (purchaseUnit.Shipping?.Name is not null)
        {
            patches.Add(new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/shipping/name",
                Value = purchaseUnit.Shipping.Name
            });
        }
        if (purchaseUnit.Shipping?.Address is not null)
        {
            patches.Add(new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/shipping/address",
                Value = purchaseUnit.Shipping.Address
            });
        }
        if (purchaseUnit.Shipping?.Options is not null)
        {
            patches.Add(new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/shipping/options",
                Value = purchaseUnit.Shipping.Options
            });
        }
        if (!string.IsNullOrEmpty(purchaseUnit.Shipping?.Type))
        {
            patches.Add(new()
            {
                Op = PatchOpType.REPLACE.ToString().ToLower(),
                Path = "/purchase_units/@reference_id=='default'/shipping/type",
                Value = purchaseUnit.Shipping.Type
            });
        }

        return patches;
    }

    /// <summary>
    /// Get customer's address (existing or a new one)
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="newAddress">Address to check</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer address
    /// </returns>
    private async Task<NopAddress> PrepareCustomerAddressAsync(Customer customer, NopAddress newAddress)
    {
        var customerAddresses = await _customerService.GetAddressesByCustomerIdAsync(customer.Id);
        var query = customerAddresses.AsQueryable();
        if (!string.IsNullOrEmpty(newAddress.Email))
            query = query.Where(address => string.Equals(address.Email, newAddress.Email));
        if (!string.IsNullOrEmpty(newAddress.FirstName))
            query = query.Where(address => string.Equals(address.FirstName, newAddress.FirstName));
        if (!string.IsNullOrEmpty(newAddress.LastName))
            query = query.Where(address => string.Equals(address.LastName, newAddress.LastName));
        if (!string.IsNullOrEmpty(newAddress.Address1))
            query = query.Where(address => string.Equals(address.Address1, newAddress.Address1));
        if (!string.IsNullOrEmpty(newAddress.Address2))
            query = query.Where(address => string.Equals(address.Address2, newAddress.Address2));
        if (!string.IsNullOrEmpty(newAddress.City))
            query = query.Where(address => string.Equals(address.City, newAddress.City));
        if (newAddress.StateProvinceId > 0)
            query = query.Where(address => address.StateProvinceId == newAddress.StateProvinceId);
        if (newAddress.CountryId > 0)
            query = query.Where(address => address.CountryId == newAddress.CountryId);
        if (!string.IsNullOrEmpty(newAddress.ZipPostalCode))
            query = query.Where(address => string.Equals(address.ZipPostalCode, newAddress.ZipPostalCode));

        var existingAddress = query.FirstOrDefault();
        if (existingAddress is not null)
            return existingAddress;

        await _addressService.InsertAddressAsync(newAddress);
        await _customerService.InsertCustomerAddressAsync(customer, newAddress);

        return newAddress;
    }

    /// <summary>
    /// Get shipping option name
    /// </summary>
    /// <param name="option">Shipping option</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping option name
    /// </returns>
    private async Task<string> GetShippingOptionNameAsync(NopShippingOption option)
    {
        return option.IsPickupInStore
            ? (string.IsNullOrEmpty(option.Name)
            ? await _localizationService.GetResourceAsync("Checkout.PickupPoints.NullName")
            : string.Format(await _localizationService.GetResourceAsync("Checkout.PickupPoints.Name"), option.Name))
            : option.Name;
    }

    #endregion

    #region Payment tokens

    /// <summary>
    /// Prepare payment tokens with additional details
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="tokens">Payment tokens</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of payment tokens
    /// </returns>
    private async Task<List<PayPalToken>> PreparePaymentTokensAsync(PayPalCommerceSettings settings, IList<PayPalToken> tokens)
    {
        var paymentTokens = new List<PaymentToken>();

        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token.VaultCustomerId))
                continue;

            //try to get payment tokens from the vault using the PayPal Server SDK
            var listInput = new SdkModels.ListCustomerPaymentTokensInput
            {
                CustomerId = token.VaultCustomerId
            };

            var listResponse = await _paypalServerSdkClient.VaultController.ListCustomerPaymentTokensAsync(listInput);
            var mappedResponse = MapCustomerVaultPaymentTokensFromServerSdk(listResponse?.Data);

            paymentTokens.AddRange(mappedResponse?.PaymentTokens ?? new());
        }
        if (paymentTokens?.Any() != true)
            return new List<PayPalToken>();

        return tokens.OrderBy(token => token.IsPrimaryMethod ? 0 : 1).ThenBy(token => token.Id).Select(paymentToken =>
        {
            var existingToken = paymentTokens
                .FirstOrDefault(token => string.Equals(token.Id, paymentToken.VaultId, StringComparison.InvariantCultureIgnoreCase));
            if (existingToken is null)
                return null;

            //set title and expiration date
            paymentToken.Title = existingToken.PaymentSource?.Card is not null
                ? $"{existingToken.PaymentSource.Card.Brand} *{existingToken.PaymentSource.Card.LastDigits}"
                : (existingToken.PaymentSource?.Venmo is not null
                ? existingToken.PaymentSource.Venmo.UserName
                : (existingToken.PaymentSource?.PayPal is not null
                ? existingToken.PaymentSource.PayPal.EmailAddress
                : "N/A"));
            paymentToken.Expiration = existingToken.PaymentSource?.Card is not null ? existingToken.PaymentSource.Card.Expiry : "N/A";

            return paymentToken;
        }).Where(token => token is not null).ToList();
    }

    /// <summary>
    /// Get the unique ID for a customer in PayPal Vault
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="customerId">Customer id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the vault customer id
    /// </returns>
    private async Task<string> GetVaultCustomerIdAsync(PayPalCommerceSettings settings, int customerId)
    {
        return (await _tokenService.GetAllTokensAsync(settings.ClientId, customerId))
            .OrderBy(token => token.IsPrimaryMethod ? 0 : 1)
            .ThenBy(token => token.Id)
            .FirstOrDefault()
            ?.VaultCustomerId;
    }

    #endregion

    #region Common

    /// <summary>
    /// Get calculated SHA256 hash for the input string
    /// </summary>
    /// <param name="stringToHash">Input string for the hash</param>
    /// <returns>SHA256 hash</returns>
    private static string GetSha256Hash(string stringToHash)
    {
        return SHA256.HashData(Encoding.Default.GetBytes(stringToHash)).Aggregate(string.Empty, (current, next) => $"{current}{next:x2}");
    }

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Convert object properties to a dictionary
    /// </summary>
    /// <param name="data">Object to convert</param>
    /// <returns>Dictionary of properties names and values</returns>
    public static Dictionary<string, string> ObjectToDictionary(object data)
    {
        return new Dictionary<string, string>(data.GetType().GetProperties().Select(property =>
        {
            var key = property
                ?.GetCustomAttributes(typeof(JsonPropertyAttribute), false).OfType<JsonPropertyAttribute>().FirstOrDefault()
                ?.PropertyName ?? string.Empty;
            var value = property.GetValue(data) is bool boolValue
                ? boolValue.ToString().ToLower()
                : property.GetValue(data)?.ToString();
            return new KeyValuePair<string, string>(key, value);
        }).Where(pair => !string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value)));
    }

    #region Configuration

    /// <summary>
    /// Check whether the plugin is configured
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>Result</returns>
    public static bool IsConfigured(PayPalCommerceSettings settings)
    {
        //client id and secret are required to request remote services
        return !string.IsNullOrEmpty(settings?.ClientId) && !string.IsNullOrEmpty(settings.SecretKey);
    }

    /// <summary>
    /// Check whether the plugin is configured and connected
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>Result</returns>
    public static bool IsConnected(PayPalCommerceSettings settings)
    {
        //webhook is required to accept notifications
        return IsConfigured(settings) && !string.IsNullOrEmpty(settings.WebhookUrl);
    }

    /// <summary>
    /// Check whether the plugin is configured, connected and active
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the check result; plugin instance
    /// </returns>
    public async Task<(bool Active, IPaymentMethod paymentMethod)> IsActiveAsync(PayPalCommerceSettings settings)
    {
        if (!IsConnected(settings))
            return (false, null);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var plugin = await _paymentPluginManager.LoadPluginBySystemNameAsync(PayPalCommerceDefaults.SystemName, customer, store.Id);
        if (!_paymentPluginManager.IsPluginActive(plugin))
            return (false, plugin);

        return (true, plugin);
    }

    /// <summary>
    /// Get access token
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the access token; error message if exists
    /// </returns>
    public async Task<(AccessToken AccessToken, string Error)> GetAccessTokenAsync(PayPalCommerceSettings settings)
    {
        return await HandleFunctionAsync(async () =>
        {
            return await _httpClient.RequestAsync<GetAccessTokenRequest, GetAccessTokenResponse>(new()
            {
                ClientId = settings.ClientId,
                Secret = settings.SecretKey,
                GrantType = "client_credentials"
            }, settings);
        });
    }

    #endregion

    #region Components

    /// <summary>
    /// Prepare details to render payment buttons/messages
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="placement">Button placement</param>
    /// <param name="productId">Product id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the script details, customer details, messages details; cart details; error message if exists
    /// </returns>
    public async Task<(((string ScriptUrl, string ClientToken, string UserToken),
        (string Email, string Name),
        (string MessageConfig, string Amount),
        (bool? IsRecurring, bool IsShippable)),
        string Error)>
        PreparePaymentDetailsAsync(PayPalCommerceSettings settings, ButtonPlacement placement, int? productId)
    {
        return await HandleFunctionAsync(async () =>
        {
            //get the primary store currency
            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
                throw new NopException("Primary store currency not set");

            //customer details
            var customer = await _workContext.GetCurrentCustomerAsync();
            var isGuest = await _customerService.IsGuestAsync(customer);
            var address = await _customerService.GetCustomerBillingAddressAsync(customer);
            var email = address is not null ? address.Email : customer.Email;
            var fullName = address is not null
                ? await _customerService.GetCustomerFullNameAsync(new() { FirstName = address.FirstName, LastName = address.LastName })
                : await _customerService.GetCustomerFullNameAsync(customer);

            //prepare script components
            var components = new List<string>() { "buttons", "funding-eligibility" };
            if (placement == ButtonPlacement.PaymentMethod && settings.UseCardFields)
                components.Add("card-fields");
            if (placement == ButtonPlacement.PaymentMethod && settings.UseAlternativePayments)
                components.Add("payment-fields");
            if (settings.UseApplePay && placement != ButtonPlacement.Product)
                components.Add("applepay");
            if (settings.UseGooglePay)
                components.Add("googlepay");
            if (settings.UseSandbox || settings.ConfiguratorSupported)
                components.Add("messages");

            var script = new Script
            {
                ClientId = settings.ClientId,
                Currency = currencyCode.ToUpper(),
                Intent = settings.PaymentType.ToString().ToLower(),
                Commit = placement == ButtonPlacement.PaymentMethod && settings.SkipOrderConfirmPage,
                Components = string.Join(',', components),
                EnableFunding = settings.EnabledFunding,
                DisableFunding = settings.DisabledFunding,
                Vault = settings.UseVault && !isGuest,
                Debug = false,
                //BuyerCountry = null,    //PayPal auto detects this
                //Locale = null,          //PayPal auto detects this
                //IntegrationDate = null  //defaults to the date when client ID was created
            };

            var scriptUrl = QueryHelpers.AddQueryString(PayPalCommerceDefaults.ServiceScriptUrl, ObjectToDictionary(script));

            //client token is required for Advanced Credit and Debit Card Payments
            string clientToken = null;
            if (placement == ButtonPlacement.PaymentMethod && settings.UseCardFields)
            {
                var identityToken = await _httpClient.RequestAsync<CreateIdentityTokenRequest, CreateIdentityTokenResponse>(new()
                {
                    CustomerId = CommonHelper.EnsureMaximumLength(GetSha256Hash(customer.CustomerGuid.ToString()), 22)
                }, settings);
                clientToken = identityToken?.ClientToken;
            }

            //user ID token is required for Vault feature
            string userToken = null;
            if (settings.UseVault && !isGuest)
            {
                var vaultCustomerId = await GetVaultCustomerIdAsync(settings, customer.Id);
                var accessToken = await _httpClient.RequestAsync<GetAccessTokenRequest, GetAccessTokenResponse>(new()
                {
                    ClientId = settings.ClientId,
                    Secret = settings.SecretKey,
                    GrantType = "client_credentials",
                    ResponseType = "id_token",
                    TargetCustomerId = vaultCustomerId
                }, settings);
                userToken = accessToken?.UserIdToken;
            }

            //Pay Later details
            var amount = await PrepareMessagesAmountAsync(placement, customer, currencyCode, productId);
            var payLaterConfig = new
            {
                cart = new MessageConfiguration(),
                product = new MessageConfiguration(),
                checkout = new MessageConfiguration()
            };
            payLaterConfig = JsonConvert.DeserializeAnonymousType(settings.PayLaterConfig ?? string.Empty, payLaterConfig);
            var config = placement switch
            {
                ButtonPlacement.Cart => payLaterConfig?.cart,
                ButtonPlacement.Product => payLaterConfig?.product,
                ButtonPlacement.PaymentMethod => payLaterConfig?.checkout,
                _ => null
            };
            var messageConfig = !string.IsNullOrEmpty(config?.Status) ? JsonConvert.SerializeObject(config, Formatting.Indented) : "{}";

            //cart details
            var (isRecurring, _) = await CheckShoppingCartIsRecurringAsync(placement, productId);
            var (isShippable, _) = await CheckShippingIsRequiredAsync(productId);

            return ((scriptUrl, clientToken, userToken), (email, fullName), (messageConfig, amount), (isRecurring, isShippable));
        });
    }

    /// <summary>
    /// Prepare details to render Pay Later messages
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="placement">Button placement</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the message configuration, amount value, currency code; error message if exists
    /// </returns>
    public async Task<((string Config, string Amount, string CurrencyCode), string Error)>
        PrepareMessagesAsync(PayPalCommerceSettings settings, ButtonPlacement placement)
    {
        return await HandleFunctionAsync(async () =>
        {
            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
                throw new NopException("Primary store currency not set");

            var customer = await _workContext.GetCurrentCustomerAsync();
            var amount = await PrepareMessagesAmountAsync(placement, customer, currencyCode, null);

            var payLaterConfig = new
            {
                cart = new MessageConfiguration(),
                product = new MessageConfiguration(),
                checkout = new MessageConfiguration()
            };
            payLaterConfig = JsonConvert.DeserializeAnonymousType(settings.PayLaterConfig ?? string.Empty, payLaterConfig);
            var config = placement switch
            {
                ButtonPlacement.Cart => payLaterConfig?.cart,
                ButtonPlacement.Product => payLaterConfig?.product,
                ButtonPlacement.PaymentMethod => payLaterConfig?.checkout,
                _ => null
            };
            var messageConfig = !string.IsNullOrEmpty(config?.Status) ? JsonConvert.SerializeObject(config, Formatting.Indented) : "{}";

            return (messageConfig, amount, currencyCode);
        });
    }

    #endregion

    #region Checkout

    /// <summary>
    /// Check whether the checkout is enabled for the customer
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the check results; shopping cart
    /// </returns>
    public async Task<(bool IsEnabled, bool LoginIsRequired, IList<ShoppingCartItem> Cart)> CheckoutIsEnabledAsync()
    {
        return (await HandleFunctionAsync(async () =>
        {
            if (_orderSettings.CheckoutDisabled)
                return (false, false, null);

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            if (!cart.Any())
                return (false, false, null);

            if (await _customerService.IsGuestAsync(customer))
            {
                if (!_orderSettings.AnonymousCheckoutAllowed)
                    return (true, true, cart);

                var downloadableProductsRequireRegistration = _customerSettings.RequireRegistrationForDownloadableProducts &&
                    await _productService.HasAnyDownloadableProductAsync(cart.Select(item => item.ProductId).ToArray());
                if (downloadableProductsRequireRegistration)
                    return (true, true, cart);
            }

            return (true, false, cart);
        }, false)).Result;
    }

    /// <summary>
    /// Check whether the shopping cart is valid
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the validation warnings; error message if exists
    /// </returns>
    public async Task<(IList<string> Warnings, string Error)> ValidateShoppingCartAsync()
    {
        return await HandleFunctionAsync(async () =>
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            if (!cart.Any())
                throw new NopException("Shopping cart is empty");

            await _customerService.ResetCheckoutDataAsync(customer, store.Id, clearShippingMethod: false);

            var checkoutAttributesXml = await _genericAttributeService
                .GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id);
            var cartWarnings = await _shoppingCartService.GetShoppingCartWarningsAsync(cart, checkoutAttributesXml, true);
            if (cartWarnings.Any())
                return cartWarnings;

            foreach (var item in cart)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);

                var itemWarnings = await _shoppingCartService
                    .GetShoppingCartItemWarningsAsync(customer, item.ShoppingCartType, product, item.StoreId, item.AttributesXml,
                    item.CustomerEnteredPrice, item.RentalStartDateUtc, item.RentalEndDateUtc, item.Quantity, false, item.Id);
                if (itemWarnings.Any())
                    return itemWarnings;
            }

            return null;
        });
    }

    /// <summary>
    /// Check whether the shipping is required for the current cart/product
    /// </summary>
    /// <param name="productId">Product id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the check result; error message if exists
    /// </returns>
    public async Task<(bool ShippingIsRequired, string Error)> CheckShippingIsRequiredAsync(int? productId)
    {
        return await HandleFunctionAsync(async () =>
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            var shippingIsRequired = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);

            if (!shippingIsRequired && await _productService.GetProductByIdAsync(productId ?? 0) is Product product)
                shippingIsRequired = product.IsShipEnabled;

            return shippingIsRequired;
        }, false);
    }

    /// <summary>
    /// Check whether the current cart/product is recurring
    /// </summary>
    /// <param name="placement">Button placement</param>
    /// <param name="productId">Product id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the check result; error message if exists
    /// </returns>
    public async Task<(bool? IsRecurring, string Error)> CheckShoppingCartIsRecurringAsync(ButtonPlacement placement, int? productId = null)
    {
        return await HandleFunctionAsync(async () =>
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            var isRecurring = await _shoppingCartService.ShoppingCartIsRecurringAsync(cart);

            if (!isRecurring && await _productService.GetProductByIdAsync(productId ?? 0) is Product product)
                isRecurring = product.IsRecurring;

            //we cannot start checkout process from the product or cart page (no way to get shipping details) for recurring items
            if (isRecurring && (placement == ButtonPlacement.Cart || placement == ButtonPlacement.Product))
                return (bool?)null;

            return isRecurring;
        }, false);
    }

    #endregion

    #region Orders

    /// <summary>
    /// Get the order by id
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="orderId">Order id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order; error message if exists
    /// </returns>
    public async Task<(Order Order, string Error)> GetOrderAsync(PayPalCommerceSettings settings, string orderId)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var paymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync()
                ?? throw new NopException("Order payment info not found");

            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                !string.Equals(orderIdValue.Value, orderId, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            var placementKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Placement");
            if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                !Enum.TryParse<ButtonPlacement>(placementValue.Value, out var placement))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            var order = await GetOrderWithServerSdkAsync(settings, orderId);

            return order;
        });
    }

    /// <summary>
    /// Get a previously created order if exists
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="paymentRequest">Payment request</param>
    /// <param name="placement">Button placement</param>
    /// <param name="shippingIsRequired">Whether the shipping is required (used for validation)</param>
    /// <param name="paymentSource">Payment source</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the created order; error message if exists
    /// </returns>
    public async Task<(Order Order, string Error)> GetCreatedOrderAsync(PayPalCommerceSettings settings,
        ProcessPaymentRequest paymentRequest, ButtonPlacement placement, bool shippingIsRequired, string paymentSource)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (paymentRequest is null)
                return null;

            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) || string.IsNullOrEmpty(orderIdValue.Value))
                return null;

            var placementKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Placement");
            if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                !Enum.TryParse<ButtonPlacement>(placementValue.Value, out var previousPlacement) ||
                previousPlacement != placement)
            {
                return null;
            }

            var order = await GetOrderWithServerSdkAsync(settings, orderIdValue.Value);

            //we cannot use completed order
            if (order.Status?.ToUpper() != OrderStatusType.CREATED.ToString() &&
                order.Status?.ToUpper() != OrderStatusType.PAYER_ACTION_REQUIRED.ToString() &&
                order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString())
            {
                return null;
            }

            //check validity interval
            if (!DateTime.TryParse(order.CreateTime, out var createTime) ||
                (DateTime.UtcNow - createTime.ToUniversalTime()).TotalSeconds > settings.OrderValidityInterval)
            {
                return null;
            }

            if (order.PurchaseUnits.FirstOrDefault() is not PurchaseUnit unit || (unit.Shipping is null != !shippingIsRequired))
                return null;

            //payment sources must match
            if (string.Equals(paymentSource, nameof(PaymentSource.PayPal), StringComparison.InvariantCultureIgnoreCase) &&
                order.PaymentSource?.PayPal is null)
            {
                return null;
            }

            if (string.Equals(paymentSource, nameof(PaymentSource.Card), StringComparison.InvariantCultureIgnoreCase) &&
                order.PaymentSource?.Card is null)
            {
                return null;
            }

            return order;
        }, false);
    }

    /// <summary>
    /// Create an order
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="placement">Button placement</param>
    /// <param name="paymentSource">Payment source</param>
    /// <param name="cardId">Saved card id</param>
    /// <param name="saveCard">Whether to save card payment token</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the created order; error message if exists
    /// </returns>
    public async Task<(Order Order, string Error)> CreateOrderAsync(PayPalCommerceSettings settings,
        ButtonPlacement placement, string paymentSource, int? cardId, bool saveCard)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            if (string.IsNullOrEmpty(settings.MerchantId))
                throw new NopException("Merchant PayPal ID not set");

            var details = await PrepareCartDetailsAsync(placement);

            var savedPaymentToken = await _tokenService.GetByIdAsync(cardId ?? 0);
            if (savedPaymentToken is not null && savedPaymentToken.CustomerId != details.Customer.Id)
                throw new NopException("Card details not found");

            var isGuest = await _customerService.IsGuestAsync(details.Customer);
            var isRecurring = await _shoppingCartService.ShoppingCartIsRecurringAsync(details.Cart);
            if (isRecurring)
            {
                if (!settings.UseVault)
                    throw new NopException("Vault disabled");

                if (isGuest)
                    throw new NopException("Anonymous checkout disabled for recurring items");

                var (error, cycleLength, cyclePeriod, totalCycles) = await _shoppingCartService.GetRecurringCycleInfoAsync(details.Cart);
                if (!string.IsNullOrEmpty(error))
                    throw new NopException(error);
            }

            var paymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync();
            var (order, _) = await GetCreatedOrderAsync(settings, paymentRequest, placement, details.ShippingIsRequired, paymentSource);
            if (paymentRequest is null || order is null)
                paymentRequest = new();

            //prepare purchase unit
            var purchaseUnit = await PreparePurchaseUnitAsync(settings, details, paymentRequest.OrderGuid.ToString());

            //whether we should create a new order
            if (order is null || isRecurring)
            {
                var isCard = string.Equals(paymentSource, nameof(PaymentSource.Card), StringComparison.InvariantCultureIgnoreCase);
                var isVenmo = string.Equals(paymentSource, nameof(PaymentSource.Venmo), StringComparison.InvariantCultureIgnoreCase);
                var isApplepay = string.Equals(paymentSource, nameof(PaymentSource.ApplePay), StringComparison.InvariantCultureIgnoreCase);
                if (isRecurring && (isVenmo || isApplepay))
                    throw new NopException($"Payment source '{paymentSource.ToUpper()}' not supported");

                var context = PrepareOrderContext(settings, details, paymentRequest.OrderGuid.ToString(), isApplepay);
                var payer = await PrepareBillingDetailsAsync(settings, details);

                //only registered customers can save payment tokens
                var vault = !settings.UseVault || isGuest ? null : new VaultInstruction
                {
                    UsageType = VaultUsageType.MERCHANT.ToString().ToUpper(),
                    CustomerType = VaultUsageType.CONSUMER.ToString().ToUpper(),
                    StoreInVault = VaultInstructionType.ON_SUCCESS.ToString().ToUpper(),
                    PermitMultiplePaymentTokens = false,
                    UsagePattern = isRecurring ? UsagePatternType.INSTALLMENT_PREPAID.ToString().ToUpper() : null
                };

                //set payment source
                var paymentSourceDetails = new PaymentSource();
                if (isCard)
                {
                    paymentSourceDetails.Card = new()
                    {
                        ExperienceContext = context,
                        BillingAddress = !string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? null : payer.Address,
                        VaultId = savedPaymentToken?.VaultId,
                        Attributes = vault is null || !saveCard || !string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? null : new()
                        {
                            Vault = vault,
                            Customer = payer
                        }
                    };

                    if (vault is not null && (saveCard || !string.IsNullOrEmpty(savedPaymentToken?.VaultId)))
                    {
                        paymentSourceDetails.Card.StoredCredential = new()
                        {
                            PaymentInitiator = PaymentInitiatorType.CUSTOMER.ToString().ToUpper(),
                            PaymentType = isRecurring
                                ? Api.Models.Enums.PaymentType.RECURRING.ToString().ToUpper()
                                : Api.Models.Enums.PaymentType.ONE_TIME.ToString().ToUpper(),
                            Usage = saveCard
                                ? StoredPaymentUsageType.FIRST.ToString().ToUpper()
                                : StoredPaymentUsageType.SUBSEQUENT.ToString().ToUpper(),
                            UsagePattern = isRecurring ? UsagePatternType.INSTALLMENT_PREPAID.ToString().ToUpper() : null
                        };
                    }

                    if (placement == ButtonPlacement.PaymentMethod && settings.UseCardFields)
                    {
                        paymentSourceDetails.Card.Attributes = new()
                        {
                            Vault = vault is not null && saveCard && string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? vault : null,
                            Customer = vault is not null && saveCard && string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? payer : null,
                            Verification = new()
                            {
                                Method = settings.CustomerAuthenticationRequired
                                    ? VerificationInstructionMethodType.SCA_ALWAYS.ToString().ToUpper()
                                    : VerificationInstructionMethodType.SCA_WHEN_REQUIRED.ToString().ToUpper()
                            }
                        };
                    }
                }
                else if (isVenmo)
                {
                    paymentSourceDetails.Venmo = new()
                    {
                        ExperienceContext = context,
                        EmailAddress = payer.EmailAddress,
                        Attributes = vault is not null ? new() { Vault = vault, Customer = payer } : null
                    };
                }
                else
                {
                    paymentSourceDetails.PayPal = new()
                    {
                        ExperienceContext = context,
                        EmailAddress = payer.EmailAddress,
                        Name = payer.Name,
                        BirthDate = payer.BirthDate,
                        Address = payer.Address,
                        Attributes = vault is not null ? new() { Vault = vault, Customer = payer } : null
                    };
                }

                var intent = MapCheckoutPaymentIntent(settings.PaymentType);

                var purchaseUnits = new List<SdkModels.PurchaseUnitRequest>();

                if (purchaseUnit is not null)
                {
                    var sdkPurchaseUnit = new SdkModels.PurchaseUnitRequest(
                        amount: MapAmountWithBreakdownToServerSdk(purchaseUnit.Amount),
                        referenceId: purchaseUnit.ReferenceId,
                        payee: MapPayeeToServerSdk(purchaseUnit.Payee),
                        paymentInstruction: MapPaymentInstructionToServerSdk(purchaseUnit.PaymentInstruction),
                        description: purchaseUnit.Description,
                        customId: purchaseUnit.CustomId,
                        invoiceId: purchaseUnit.InvoiceId,
                        softDescriptor: purchaseUnit.SoftDescriptor,
                        items: purchaseUnit.Items?.Select(MapItemToServerSdk).Where(item => item is not null).ToList(),
                        shipping: MapShippingToServerSdk(purchaseUnit.Shipping),
                        supplementaryData: MapSupplementaryDataToServerSdk(purchaseUnit.SupplementaryData));

                    if (sdkPurchaseUnit is not null)
                        purchaseUnits.Add(sdkPurchaseUnit);
                }

                SdkModels.PaymentSource sdkPaymentSource = null;
                if (paymentSourceDetails is not null)
                {
                    sdkPaymentSource = new SdkModels.PaymentSource();

                    if (paymentSourceDetails.Card is not null)
                        sdkPaymentSource.Card = MapCardToServerSdk(paymentSourceDetails.Card);

                    if (paymentSourceDetails.PayPal is not null)
                        sdkPaymentSource.Paypal = MapPaypalWalletToServerSdk(paymentSourceDetails.PayPal);

                    if (paymentSourceDetails.Venmo is not null)
                        sdkPaymentSource.Venmo = MapVenmoWalletToServerSdk(paymentSourceDetails.Venmo);
                }

                var orderRequest = new SdkModels.OrderRequest(
                    intent: intent,
                    purchaseUnits: purchaseUnits,
                    payer: null,
                    paymentSource: sdkPaymentSource,
                    applicationContext: null);

                order = await CreateOrderWithServerSdkAsync(settings, orderRequest);
            }
            else
            {
                //order exists, so just update some details
                var patches = PreparePatches(purchaseUnit);
                patches.Add(new()
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/intent",
                    Value = settings.PaymentType.ToString().ToUpper()
                });
                await PatchOrderWithServerSdkAsync(settings, order.Id, patches);
            }

            //save order details for future using as the payment request
            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            paymentRequest.CustomValues[orderIdKey] = order.Id;

            var placementKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Placement");
            paymentRequest.CustomValues.Remove(placementKey);
            paymentRequest.CustomValues.Add(new(placementKey, placement.ToString(), displayToCustomer: false));

            if (isRecurring && !string.IsNullOrEmpty(savedPaymentToken?.VaultId))
            {
                paymentRequest.CustomValues.Remove(PayPalCommerceDefaults.TokenIdAttributeName);
                paymentRequest.CustomValues.Add(new(PayPalCommerceDefaults.TokenIdAttributeName, savedPaymentToken.Id.ToString(), displayToCustomer: false));
            }

            await _orderProcessingService.SetProcessPaymentRequestAsync(paymentRequest, true);

            return order;
        });
    }

    private async Task<Order> CreateOrderWithServerSdkAsync(PayPalCommerceSettings settings, SdkModels.OrderRequest orderRequest)
    {
        if (orderRequest is null)
            throw new ArgumentNullException(nameof(orderRequest));

        var input = new SdkModels.CreateOrderInput(
            contentType: "application/json",
            body: orderRequest,
            paypalRequestId: Guid.NewGuid().ToString(),
            paypalPartnerAttributionId: PayPalCommerceDefaults.PartnerHeader.Value,
            prefer: "return=representation");

        var response = await _paypalServerSdkClient.OrdersController.CreateOrderAsync(input);
        if (response?.Data is null)
            throw new NopException("Failed to read PayPal order data.");

        var sdkOrder = response.Data;
        var order = MapOrderFromServerSdk<SdkModels.Order>(sdkOrder);
        if (order is null)
            throw new NopException("Failed to map PayPal order response.");

        return order;
    }

    private async Task<Order> GetOrderWithServerSdkAsync(PayPalCommerceSettings settings, string orderId)
    {
        if (string.IsNullOrEmpty(orderId))
            throw new ArgumentException("Order ID is required.", nameof(orderId));

        var input = new SdkModels.GetOrderInput(id: orderId);

        var response = await _paypalServerSdkClient.OrdersController.GetOrderAsync(input);
        if (response?.Data is null)
            throw new NopException("Failed to read PayPal order data.");

        var sdkOrder = response.Data;
        var order = MapOrderFromServerSdk<SdkModels.Order>(sdkOrder);
        if (order is null)
            throw new NopException("Failed to map PayPal order response.");

        return order;
    }

    private async Task<Order> AuthorizeOrderWithServerSdkAsync(PayPalCommerceSettings settings, string orderId)
    {
        if (string.IsNullOrEmpty(orderId))
            throw new ArgumentNullException(nameof(orderId));

        var input = new SdkModels.AuthorizeOrderInput(
            id: orderId,
            contentType: "application/json",
            paypalRequestId: Guid.NewGuid().ToString(),
            prefer: "return=representation");

        var response = await _paypalServerSdkClient.OrdersController.AuthorizeOrderAsync(input);
        if (response?.Data is null)
            throw new NopException("Failed to read PayPal order data.");

        var sdkOrder = response.Data;
        var order = MapOrderFromServerSdk<SdkModels.OrderAuthorizeResponse>(sdkOrder);
        if (order is null)
            throw new NopException("Failed to map PayPal order response.");

        return order;
    }

    private async Task<Order> CaptureOrderWithServerSdkAsync(PayPalCommerceSettings settings, string orderId)
    {
        if (string.IsNullOrEmpty(orderId))
            throw new ArgumentNullException(nameof(orderId));

        var input = new SdkModels.CaptureOrderInput(
            id: orderId,
            contentType: "application/json",
            paypalRequestId: Guid.NewGuid().ToString(),
            prefer: "return=representation");

        var response = await _paypalServerSdkClient.OrdersController.CaptureOrderAsync(input);
        if (response?.Data is null)
            throw new NopException("Failed to read PayPal order data.");

        var sdkOrder = response.Data;
        var order = MapOrderFromServerSdk<SdkModels.Order>(sdkOrder);
        if (order is null)
            throw new NopException("Failed to map PayPal order response.");

        return order;
    }
    private async Task PatchOrderWithServerSdkAsync(PayPalCommerceSettings settings, string orderId, IEnumerable<Patch<object>> patches)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrEmpty(orderId))
            throw new ArgumentNullException(nameof(orderId));

        if (patches is null)
            throw new ArgumentNullException(nameof(patches));

        var sdkPatches = MapPatchesToServerSdk(patches);
        if (sdkPatches is null || sdkPatches.Count == 0)
            return;

        var input = new SdkModels.PatchOrderInput(
            id: orderId,
            contentType: "application/json",
            paypalMockResponse: null,
            paypalAuthAssertion: null,
            body: sdkPatches);

        await _paypalServerSdkClient.OrdersController.PatchOrderAsync(input);
    }

    private static List<SdkModels.Patch> MapPatchesToServerSdk(IEnumerable<Patch<object>> patches)
    {
        if (patches is null)
            return null;

        var sdkPatches = new List<SdkModels.Patch>();

        foreach (var patch in patches)
        {
            if (patch is null)
                continue;

            var op = MapPatchOpToServerSdk(patch.Op);

            JsonValue value = null;
            if (patch.Value is not null)
                value = JsonValue.FromObject(patch.Value);

            sdkPatches.Add(new SdkModels.Patch(
                op: op,
                path: patch.Path,
                mValue: value,
                from: patch.From));
        }

        return sdkPatches;
    }

    private static SdkModels.PatchOp MapPatchOpToServerSdk(string op)
    {
        if (string.IsNullOrEmpty(op))
            throw new ArgumentNullException(nameof(op));

        switch (op.ToLowerInvariant())
        {
            case "add":
                return SdkModels.PatchOp.Add;
            case "remove":
                return SdkModels.PatchOp.Remove;
            case "replace":
                return SdkModels.PatchOp.Replace;
            case "move":
                return SdkModels.PatchOp.Move;
            case "copy":
                return SdkModels.PatchOp.Copy;
            case "test":
                return SdkModels.PatchOp.Test;
            default:
                throw new NopException($"Unsupported patch operation '{op}'.");
        }
    }

    private static SdkModels.CheckoutPaymentIntent MapCheckoutPaymentIntent(PaymentType paymentType)
    {
        return paymentType switch
        {
            PaymentType.Capture => SdkModels.CheckoutPaymentIntent.Capture,
            PaymentType.Authorize => SdkModels.CheckoutPaymentIntent.Authorize,
            _ => SdkModels.CheckoutPaymentIntent.Capture
        };
    }

    private static SdkModels.AmountWithBreakdown MapAmountWithBreakdownToServerSdk(OrderMoney amount)
    {
        if (amount is null)
            return null;

        var breakdown = amount.Breakdown is null
            ? null
            : new SdkModels.AmountBreakdown(
                itemTotal: MapMoneyToServerSdk(amount.Breakdown.ItemTotal),
                shipping: MapMoneyToServerSdk(amount.Breakdown.Shipping),
                handling: MapMoneyToServerSdk(amount.Breakdown.Handling),
                taxTotal: MapMoneyToServerSdk(amount.Breakdown.TaxTotal),
                insurance: MapMoneyToServerSdk(amount.Breakdown.Insurance),
                shippingDiscount: MapMoneyToServerSdk(amount.Breakdown.ShippingDiscount),
                discount: MapMoneyToServerSdk(amount.Breakdown.Discount));

        return new SdkModels.AmountWithBreakdown(
            currencyCode: amount.CurrencyCode,
            mValue: amount.Value,
            breakdown: breakdown);
    }

    private static SdkModels.Money MapMoneyToServerSdk(Money money)
    {
        return money is null
            ? null
            : new SdkModels.Money(money.CurrencyCode, money.Value);
    }

    private static SdkModels.PayeeBase MapPayeeToServerSdk(Payee payee)
    {
        if (payee is null)
            return null;

        return new SdkModels.PayeeBase(
            emailAddress: payee.EmailAddress,
            merchantId: payee.MerchantId);
    }

    private static SdkModels.PaymentInstruction MapPaymentInstructionToServerSdk(PaymentInstruction instruction)
    {
        if (instruction is null)
            return null;

        var platformFees = instruction.PlatformFees?
            .Select(fee => new SdkModels.PlatformFee(
                amount: MapMoneyToServerSdk(fee.Amount),
                payee: MapPayeeToServerSdk(fee.Payee)))
            .Where(fee => fee.Amount is not null)
            .ToList();

        SdkModels.DisbursementMode? disbursementMode = null;
        if (!string.IsNullOrEmpty(instruction.DisbursementMode) &&
            Enum.TryParse(instruction.DisbursementMode, ignoreCase: true, out SdkModels.DisbursementMode parsedMode))
        {
            disbursementMode = parsedMode;
        }

        return new SdkModels.PaymentInstruction(
            platformFees: platformFees,
            disbursementMode: disbursementMode,
            payeePricingTierId: instruction.PayeePricingTierId,
            payeeReceivableFxRateId: instruction.PayeeReceivableFxRateId);
    }

    private static SdkModels.ItemRequest MapItemToServerSdk(Item item)
    {
        if (item is null)
            return null;

        var unitAmount = MapMoneyToServerSdk(item.UnitAmount);
        if (unitAmount is null || string.IsNullOrEmpty(item.Name) || string.IsNullOrEmpty(item.Quantity))
            return null;

        SdkModels.ItemCategory? category = null;
        if (!string.IsNullOrEmpty(item.Category) &&
            Enum.TryParse(item.Category, ignoreCase: true, out SdkModels.ItemCategory parsedCategory))
        {
            category = parsedCategory;
        }

        SdkModels.UniversalProductCode upc = null;
        if (item.Upc is not null)
        {
            if (!Enum.TryParse<SdkModels.UpcType>(item.Upc.Type, out var upcType))
                throw new NopException("Invalid UniversalProductCode Type!");

            upc = new SdkModels.UniversalProductCode(
                type: upcType,
                code: item.Upc.Code);
        }

        return new SdkModels.ItemRequest(
            name: item.Name,
            unitAmount: unitAmount,
            quantity: item.Quantity,
            tax: MapMoneyToServerSdk(item.Tax),
            description: item.Description,
            sku: item.Sku,
            url: item.Url,
            category: category,
            imageUrl: item.ImageUrl,
            upc: upc,
            billingPlan: null);
    }

    private static SdkModels.LineItem MapLineItemToServerSdk(Item item)
    {
        if (item is null)
            return null;

        var unitAmount = MapMoneyToServerSdk(item.UnitAmount);
        if (unitAmount is null || string.IsNullOrEmpty(item.Name) || string.IsNullOrEmpty(item.Quantity))
            return null;

        SdkModels.UniversalProductCode upc = null;
        if (item.Upc is not null)
        {
            if (!Enum.TryParse<SdkModels.UpcType>(item.Upc.Type, out var upcType))
                throw new NopException("Invalid UniversalProductCode Type!");

            upc = new SdkModels.UniversalProductCode(
                type: upcType,
                code: item.Upc.Code);
        }

        return new SdkModels.LineItem(
            name: item.Name,
            quantity: item.Quantity,
            description: item.Description,
            sku: item.Sku,
            url: item.Url,
            imageUrl: item.ImageUrl,
            upc: upc,
            billingPlan: null,
            unitAmount: unitAmount,
            tax: MapMoneyToServerSdk(item.Tax),
            commodityCode: item.CommodityCode,
            discountAmount: MapMoneyToServerSdk(item.DiscountAmount),
            totalAmount: MapMoneyToServerSdk(item.TotalAmount),
            unitOfMeasure: item.UnitOfMeasure);
    }

    private static SdkModels.ShippingDetails MapShippingToServerSdk(Shipping shipping)
    {
        if (shipping is null)
            return null;

        SdkModels.FulfillmentType? type = null;
        if (!string.IsNullOrEmpty(shipping.Type) &&
            Enum.TryParse(shipping.Type, ignoreCase: true, out SdkModels.FulfillmentType parsedType))
        {
            type = parsedType;
        }

        var options = shipping.Options?
            .Select(MapShippingOptionToServerSdk)
            .Where(option => option is not null)
            .ToList();

        return new SdkModels.ShippingDetails(
            name: shipping.Name is null ? null : new SdkModels.ShippingName(shipping.Name.FullName),
            emailAddress: null,
            phoneNumber: null,
            type: type,
            options: options,
            address: MapAddressToServerSdk(shipping.Address));
    }

    private static SdkModels.ShippingOption MapShippingOptionToServerSdk(ShippingOption option)
    {
        if (option is null || string.IsNullOrEmpty(option.Id) || string.IsNullOrEmpty(option.Label))
            return null;

        SdkModels.ShippingType? type = null;
        if (!string.IsNullOrEmpty(option.Type) &&
            Enum.TryParse(option.Type, ignoreCase: true, out SdkModels.ShippingType parsedType))
        {
            type = parsedType;
        }

        return new SdkModels.ShippingOption(
            id: option.Id,
            label: option.Label,
            selected: option.Selected ?? false,
            type: type,
            amount: MapMoneyToServerSdk(option.Amount));
    }

    private static SdkModels.Address MapAddressToServerSdk(Address address)
    {
        if (address is null || string.IsNullOrEmpty(address.CountryCode))
            return null;

        return new SdkModels.Address(
            countryCode: address.CountryCode,
            addressLine1: address.AddressLine1,
            addressLine2: address.AddressLine2,
            adminArea2: address.AdminArea2,
            adminArea1: address.AdminArea1,
            postalCode: address.PostalCode);
    }

    private static SdkModels.SupplementaryData MapSupplementaryDataToServerSdk(SupplementaryData supplementary)
    {
        if (supplementary?.Card is null)
            return null;

        var level2 = supplementary.Card.Level2 is null
            ? null
            : new SdkModels.Level2CardProcessingData(
                invoiceId: supplementary.Card.Level2.InvoiceId,
                taxTotal: MapMoneyToServerSdk(supplementary.Card.Level2.TaxTotal));

        var level3 = supplementary.Card.Level3 is null
            ? null
            : new SdkModels.Level3CardProcessingData(
                shippingAmount: MapMoneyToServerSdk(supplementary.Card.Level3.ShippingAmount),
                dutyAmount: MapMoneyToServerSdk(supplementary.Card.Level3.DutyAmount),
                discountAmount: MapMoneyToServerSdk(supplementary.Card.Level3.DiscountAmount),
                shippingAddress: MapAddressToServerSdk(supplementary.Card.Level3.ShippingAddress),
                shipsFromPostalCode: supplementary.Card.Level3.ShipsFromPostalCode,
                lineItems: supplementary.Card.Level3.LineItems?
                    .Select(MapLineItemToServerSdk)
                    .Where(item => item is not null)
                    .ToList());

        var cardSupplementary = new SdkModels.CardSupplementaryData(
            level2: level2,
            level3: level3);

        return new SdkModels.SupplementaryData(card: cardSupplementary);
    }

    private static SdkModels.CardRequest MapCardToServerSdk(Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.PaymentSources.Card card)
    {
        if (card is null)
            return null;

        SdkModels.NetworkToken sdkNetworkToken = null;
        if (card.NetworkToken is not null)
        {
            SdkModels.EciFlag? eciFlag = null;
            if (!string.IsNullOrEmpty(card.NetworkToken.EciFlag) &&
                Enum.TryParse(card.NetworkToken.EciFlag, ignoreCase: true, out SdkModels.EciFlag parsedEci))
            {
                eciFlag = parsedEci;
            }

            sdkNetworkToken = new SdkModels.NetworkToken(
                number: card.NetworkToken.Number,
                expiry: card.NetworkToken.Expiry,
                cryptogram: card.NetworkToken.Cryptogram,
                eciFlag: eciFlag,
                tokenRequestorId: card.NetworkToken.TokenRequestorId);
        }

        return new SdkModels.CardRequest(
            name: card.Name,
            number: card.Number,
            expiry: card.Expiry,
            securityCode: card.SecurityCode,
            billingAddress: MapAddressToServerSdk(card.BillingAddress),
            attributes: MapCardAttributesToServerSdk(card.Attributes),
            vaultId: card.VaultId,
            singleUseToken: null,
            storedCredential: MapCardStoredCredentialToServerSdk(card.StoredCredential),
            networkToken: sdkNetworkToken,
            experienceContext: card.ExperienceContext is null
                ? null
                : new SdkModels.CardExperienceContext(
                    returnUrl: card.ExperienceContext.ReturnUrl,
                    cancelUrl: card.ExperienceContext.CancelUrl));
    }

    private static SdkModels.CardAttributes MapCardAttributesToServerSdk(Attributes attributes)
    {
        if (attributes is null)
            return null;

        var customer = attributes.Customer is null
            ? null
            : new SdkModels.CardCustomerInformation(
                id: attributes.Customer.Id,
                emailAddress: attributes.Customer.EmailAddress,
                phone: null,
                name: MapNameToServerSdk(attributes.Customer.Name),
                merchantCustomerId: attributes.Customer.MerchantCustomerId);

        var vaultInstruction = attributes.Vault is null
            ? null
            : new SdkModels.VaultInstructionBase(
                storeInVault: MapStoreInVaultInstruction(attributes.Vault.StoreInVault));

        var verification = attributes.Verification is null
            ? null
            : new SdkModels.CardVerification(
                method: MapCardVerificationMethod(attributes.Verification.Method));

        return new SdkModels.CardAttributes(
            customer: customer,
            vault: vaultInstruction,
            verification: verification);
    }

    private static SdkModels.StoreInVaultInstruction? MapStoreInVaultInstruction(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return Enum.TryParse(value, ignoreCase: true, out SdkModels.StoreInVaultInstruction parsed)
            ? parsed
            : null;
    }

    private static SdkModels.OrdersCardVerificationMethod? MapCardVerificationMethod(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return Enum.TryParse(value, ignoreCase: true, out SdkModels.OrdersCardVerificationMethod parsed)
            ? parsed
            : null;
    }

    private static SdkModels.CardStoredCredential MapCardStoredCredentialToServerSdk(StoredCredential stored)
    {
        if (stored is null ||
            string.IsNullOrEmpty(stored.PaymentInitiator) ||
            string.IsNullOrEmpty(stored.PaymentType))
        {
            return null;
        }

        if (!Enum.TryParse(stored.PaymentInitiator, ignoreCase: true, out SdkModels.PaymentInitiator initiator))
            initiator = SdkModels.PaymentInitiator.Customer;

        if (!Enum.TryParse(stored.PaymentType, ignoreCase: true, out SdkModels.StoredPaymentSourcePaymentType paymentType))
            paymentType = SdkModels.StoredPaymentSourcePaymentType.OneTime;

        SdkModels.StoredPaymentSourceUsageType? usage = null;
        if (!string.IsNullOrEmpty(stored.Usage) &&
            Enum.TryParse(stored.Usage, ignoreCase: true, out SdkModels.StoredPaymentSourceUsageType usageParsed))
        {
            usage = usageParsed;
        }

        SdkModels.NetworkTransaction previousNetworkTransaction = null;
        if (stored.PreviousNetworkTransactionReference is not null)
        {
            SdkModels.CardBrand? network = null;
            if (!string.IsNullOrEmpty(stored.PreviousNetworkTransactionReference.Network) &&
                Enum.TryParse(stored.PreviousNetworkTransactionReference.Network, ignoreCase: true, out SdkModels.CardBrand parsedBrand))
            {
                network = parsedBrand;
            }

            previousNetworkTransaction = new SdkModels.NetworkTransaction(
                id: stored.PreviousNetworkTransactionReference.Id,
                date: stored.PreviousNetworkTransactionReference.Date,
                network: network,
                acquirerReferenceNumber: stored.PreviousNetworkTransactionReference.AcquirerReferenceNumber);
        }

        return new SdkModels.CardStoredCredential(
            paymentInitiator: initiator,
            paymentType: paymentType,
            usage: usage,
            previousNetworkTransactionReference: previousNetworkTransaction);
    }

    private static SdkModels.PaypalWallet MapPaypalWalletToServerSdk(Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.PaymentSources.PayPal wallet)
    {
        if (wallet is null)
            return null;

        return new SdkModels.PaypalWallet(
            vaultId: wallet.VaultId,
            emailAddress: wallet.EmailAddress,
            name: MapNameToServerSdk(wallet.Name),
            phone: null,
            birthDate: wallet.BirthDate,
            taxInfo: null,
            address: MapAddressToServerSdk(wallet.Address),
            attributes: MapPaypalWalletAttributesToServerSdk(wallet.Attributes),
            experienceContext: MapPaypalExperienceContextToServerSdk(wallet.ExperienceContext),
            billingAgreementId: wallet.BillingAgreementId,
            storedCredential: null);
    }

    private static SdkModels.Name MapNameToServerSdk(Name name)
    {
        if (name is null)
            return null;

        return new SdkModels.Name(
            givenName: name.GivenName,
            surname: name.Surname);
    }

    private static SdkModels.PaypalWalletAttributes MapPaypalWalletAttributesToServerSdk(Attributes attributes)
    {
        if (attributes is null)
            return null;

        var customer = attributes.Customer is null
            ? null
            : new SdkModels.PaypalWalletCustomerRequest(
                id: attributes.Customer.Id,
                emailAddress: attributes.Customer.EmailAddress,
                phone: null,
                name: MapNameToServerSdk(attributes.Customer.Name),
                merchantCustomerId: attributes.Customer.MerchantCustomerId);

        var vaultInstruction = attributes.Vault is null
            ? null
            : MapPaypalVaultInstructionToServerSdk(attributes.Vault as VaultInstruction);

        return new SdkModels.PaypalWalletAttributes(
            customer: customer,
            vault: vaultInstruction);
    }

    private static SdkModels.PaypalWalletVaultInstruction MapPaypalVaultInstructionToServerSdk(VaultInstruction instruction)
    {
        if (instruction is null)
            return null;

        SdkModels.UsagePattern? usagePattern = null;
        if (!string.IsNullOrEmpty(instruction.UsagePattern) &&
            Enum.TryParse(instruction.UsagePattern, ignoreCase: true, out SdkModels.UsagePattern usageParsed))
        {
            usagePattern = usageParsed;
        }

        var usageType = instruction.UsageType?.ToUpperInvariant() switch
        {
            nameof(VaultUsageType.MERCHANT) => SdkModels.PaypalPaymentTokenUsageType.Merchant,
            nameof(VaultUsageType.PLATFORM) => SdkModels.PaypalPaymentTokenUsageType.Platform,
            _ => SdkModels.PaypalPaymentTokenUsageType.Merchant
        };

        SdkModels.PaypalPaymentTokenCustomerType? customerType = instruction.CustomerType?.ToUpperInvariant() switch
        {
            nameof(VaultUsageType.CONSUMER) => SdkModels.PaypalPaymentTokenCustomerType.Consumer,
            nameof(VaultUsageType.BUSINESS) => SdkModels.PaypalPaymentTokenCustomerType.Business,
            _ => null
        };

        return new SdkModels.PaypalWalletVaultInstruction(usageType: usageType)
        {
            Description = instruction.Description,
            UsagePattern = usagePattern,
            CustomerType = customerType,
            PermitMultiplePaymentTokens = instruction.PermitMultiplePaymentTokens
        };
    }

    private static SdkModels.PaypalWalletExperienceContext MapPaypalExperienceContextToServerSdk(ExperienceContext context)
    {
        if (context is null)
            return null;

        SdkModels.PaypalWalletContextShippingPreference? shippingPreference = null;
        if (!string.IsNullOrEmpty(context.ShippingPreference) &&
            Enum.TryParse(context.ShippingPreference, ignoreCase: true, out SdkModels.PaypalWalletContextShippingPreference shippingParsed))
        {
            shippingPreference = shippingParsed;
        }

        SdkModels.PaypalExperienceLandingPage? landingPage = null;
        if (!string.IsNullOrEmpty(context.LandingPage) &&
            Enum.TryParse(context.LandingPage, ignoreCase: true, out SdkModels.PaypalExperienceLandingPage landingParsed))
        {
            landingPage = landingParsed;
        }

        SdkModels.PaypalExperienceUserAction? userAction = null;
        if (!string.IsNullOrEmpty(context.UserAction) &&
            Enum.TryParse(context.UserAction, ignoreCase: true, out SdkModels.PaypalExperienceUserAction userParsed))
        {
            userAction = userParsed;
        }

        SdkModels.PayeePaymentMethodPreference? paymentMethodPreference = null;
        if (!string.IsNullOrEmpty(context.PaymentMethodPreference) &&
            Enum.TryParse(context.PaymentMethodPreference, ignoreCase: true, out SdkModels.PayeePaymentMethodPreference methodParsed))
        {
            paymentMethodPreference = methodParsed;
        }

        return new SdkModels.PaypalWalletExperienceContext(
            brandName: context.BrandName,
            locale: context.Locale,
            shippingPreference: shippingPreference,
            contactPreference: null,
            returnUrl: context.ReturnUrl,
            cancelUrl: context.CancelUrl,
            appSwitchContext: null,
            landingPage: landingPage,
            userAction: userAction,
            paymentMethodPreference: paymentMethodPreference,
            orderUpdateCallbackConfig: null);
    }

    private static SdkModels.VenmoWalletRequest MapVenmoWalletToServerSdk(Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.PaymentSources.Venmo venmo)
    {
        if (venmo is null)
            return null;

        return new SdkModels.VenmoWalletRequest(
            vaultId: venmo.VaultId,
            emailAddress: venmo.EmailAddress,
            experienceContext: new SdkModels.VenmoWalletExperienceContext(
                brandName: venmo.ExperienceContext?.BrandName,
                shippingPreference: null,
                orderUpdateCallbackConfig: null,
                userAction: null),
            attributes: new SdkModels.VenmoWalletAdditionalAttributes(
                customer: venmo.Attributes?.Customer is null
                    ? null
                    : new SdkModels.VenmoWalletCustomerInformation(
                        id: venmo.Attributes.Customer.Id,
                        emailAddress: venmo.Attributes.Customer.EmailAddress,
                        phone: null,
                        name: MapNameToServerSdk(venmo.Attributes.Customer.Name)),
                vault: venmo.Attributes?.Vault is null
                    ? null
                    : MapVenmoVaultAttributesToServerSdk(venmo.Attributes.Vault)));
    }

    private static SdkModels.VenmoWalletVaultAttributes MapVenmoVaultAttributesToServerSdk(VaultInstruction instruction)
    {
        if (instruction is null)
            return null;

        var storeInVault = MapStoreInVaultInstruction(instruction.StoreInVault) ?? SdkModels.StoreInVaultInstruction.OnSuccess;

        SdkModels.VenmoPaymentTokenUsagePattern? usagePattern = null;
        if (!string.IsNullOrEmpty(instruction.UsagePattern) &&
            Enum.TryParse(instruction.UsagePattern, ignoreCase: true, out SdkModels.VenmoPaymentTokenUsagePattern usagePatternParsed))
        {
            usagePattern = usagePatternParsed;
        }

        var usageType = instruction.UsageType?.ToUpperInvariant() switch
        {
            nameof(VaultUsageType.MERCHANT) => SdkModels.VenmoPaymentTokenUsageType.Merchant,
            nameof(VaultUsageType.PLATFORM) => SdkModels.VenmoPaymentTokenUsageType.Platform,
            _ => SdkModels.VenmoPaymentTokenUsageType.Merchant
        };

        SdkModels.VenmoPaymentTokenCustomerType? customerType = instruction.CustomerType?.ToUpperInvariant() switch
        {
            nameof(VaultUsageType.CONSUMER) => SdkModels.VenmoPaymentTokenCustomerType.Consumer,
            nameof(VaultUsageType.BUSINESS) => SdkModels.VenmoPaymentTokenCustomerType.Business,
            _ => null
        };

        return new SdkModels.VenmoWalletVaultAttributes(
            storeInVault: storeInVault,
            usageType: usageType,
            description: instruction.Description,
            usagePattern: usagePattern,
            customerType: customerType,
            permitMultiplePaymentTokens: instruction.PermitMultiplePaymentTokens);
    }

    private static Order MapOrderFromServerSdk<T>(T sdkOrder)
    {
        if (sdkOrder is null)
            return null;

        var serializedOrder = JsonConvert.SerializeObject(sdkOrder);
        return JsonConvert.DeserializeObject<Order>(serializedOrder);
    }

    private static PaymentToken MapPaymentTokenFromServerSdk(SdkModels.PaymentTokenResponse sdkPaymentToken)
    {
        if (sdkPaymentToken is null)
            return null;

        var serializedToken = JsonConvert.SerializeObject(sdkPaymentToken);
        return JsonConvert.DeserializeObject<PaymentToken>(serializedToken);
    }

    private static PaymentToken MapSetupTokenFromServerSdk(SdkModels.SetupTokenResponse sdkSetupToken)
    {
        if (sdkSetupToken is null)
            return null;

        var serializedToken = JsonConvert.SerializeObject(sdkSetupToken);
        return JsonConvert.DeserializeObject<PaymentToken>(serializedToken);
    }

    private static GetPaymentTokensResponse MapCustomerVaultPaymentTokensFromServerSdk(SdkModels.CustomerVaultPaymentTokensResponse sdkResponse)
    {
        if (sdkResponse is null)
            return null;

        var serializedResponse = JsonConvert.SerializeObject(sdkResponse);
        return JsonConvert.DeserializeObject<GetPaymentTokensResponse>(serializedResponse);
    }

    /// <summary>
    /// Update order shipping details
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="orderId">Order id</param>
    /// <param name="selectedAddress">Selected shipping address</param>
    /// <param name="selectedOption">Selected shipping option</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result of update; error message if exists
    /// </returns>
    public async Task<(bool Result, string Error)> UpdateOrderShippingAsync(PayPalCommerceSettings settings, string orderId,
        (string City, string State, string Country, string PostalCode) selectedAddress,
        (string Id, string Type) selectedOption)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
                throw new NopException("Primary store currency not set");

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            if (!cart.Any())
                throw new NopException("Shopping cart is empty");

            var paymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync()
                ?? throw new NopException("Order payment info not found");

            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                (!string.IsNullOrEmpty(orderId) && !string.Equals(orderIdValue.Value, orderId, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            var placementKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Placement");
            if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                !Enum.TryParse<ButtonPlacement>(placementValue.Value, out var placement))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            //changing shipping address or option on the payment method page is not available
            if (placement == ButtonPlacement.PaymentMethod)
                return false;

            //check the order status
            var order = await GetOrderWithServerSdkAsync(settings, orderIdValue.Value);
            if (order.Status?.ToUpper() != OrderStatusType.CREATED.ToString() &&
                order.Status?.ToUpper() != OrderStatusType.PAYER_ACTION_REQUIRED.ToString() &&
                order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString())
            {
                throw new NopException($"Order is in '{order.Status}' status");
            }

            if (order.PurchaseUnits.FirstOrDefault() is not PurchaseUnit unit ||
                !string.Equals(unit.CustomId, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            var shippingIsRequired = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
            if (!shippingIsRequired)
                return false;

            //update shipping details
            var details = new CartDetails
            {
                Placement = placement,
                Customer = customer,
                Store = store,
                Cart = cart.ToList(),
                CurrencyCode = currencyCode,
                ShippingIsRequired = shippingIsRequired
            };
            var shipping = await PrepareUpdatedShippingAsync(details, order.Payer?.EmailAddress, selectedAddress, selectedOption);
            if (shipping is null)
                return false;

            //recalculate the total and update the items, since the shipping price may have changed
            var items = await PrepareOrderItemsAsync(details);
            var orderAmount = await PrepareOrderMoneyAsync(details, items);
            var cardData = new CardData
            {
                Level2 = new() { InvoiceId = CommonHelper.EnsureMaximumLength(paymentRequest.OrderGuid.ToString(), 127) },
                Level3 = new()
                {
                    LineItems = items,
                    ShippingAmount = orderAmount.Breakdown.Shipping,
                    ShippingAddress = shipping?.Address,
                    ShipsFromPostalCode = CommonHelper.EnsureMaximumLength((await _addressService
                        .GetAddressByIdAsync(_shippingSettings.ShippingOriginAddressId))?.ZipPostalCode, 60)
                },
            };

            var patches = PreparePatches(new()
            {
                Shipping = shipping,
                Items = items,
                Amount = orderAmount,
                SupplementaryData = new() { Card = cardData }
            });
            await PatchOrderWithServerSdkAsync(settings, order.Id, patches);

            return true;
        });
    }

    /// <summary>
    /// The method is called after the customer approves the transaction
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="orderId">Order id</param>
    /// <param name="orderGuid">Internal order id</param>
    /// <param name="liabilityShift">Liability shift</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order; whether to process the payment immediately; error message if exists
    /// </returns>
    public async Task<((Order Order, bool PayNow), string Error)>
        OrderIsApprovedAsync(PayPalCommerceSettings settings, string orderId, string orderGuid, string liabilityShift)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
                throw new NopException("Primary store currency not set");

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            if (!cart.Any())
                throw new NopException("Shopping cart is empty");

            var paymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync()
                ?? throw new NopException("Order payment info not found");

            if (!string.IsNullOrEmpty(orderGuid) &&
                !string.Equals(orderGuid, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                (!string.IsNullOrEmpty(orderId) && !string.Equals(orderIdValue.Value, orderId, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            var placementKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Placement");
            if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                !Enum.TryParse<ButtonPlacement>(placementValue.Value, out var placement))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            //check the order status
            var order = await GetOrderWithServerSdkAsync(settings, orderIdValue.Value);
            if (order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString() && order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
            {
                if (order.Status?.ToUpper() == OrderStatusType.CREATED.ToString())
                {
                    if (liabilityShift?.ToUpper() == LiabilityShiftType.NO.ToString())
                        throw new NopException($"3D Secure contingency is not resolved");

                    if (liabilityShift?.ToUpper() == LiabilityShiftType.UNKNOWN.ToString())
                        throw new NopException($"The authentication system isn't available, please retry later");
                }
                else
                    throw new NopException($"Order is in '{order.Status}' status");
            }

            if (order.PurchaseUnits.FirstOrDefault() is not PurchaseUnit unit ||
                !string.Equals(unit.CustomId, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            await _genericAttributeService
                .SaveAttributeAsync(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, PayPalCommerceDefaults.SystemName, store.Id);

            //place order immediately, once order is completed
            if (order.Status.ToUpper() == OrderStatusType.COMPLETED.ToString())
                return (order, true);

            var pickupPoint = await _genericAttributeService
                .GetAttributeAsync<PickupPoint>(customer, NopCustomerDefaults.SelectedPickupPointAttribute, store.Id);
            var pickupInStore = _shippingSettings.AllowPickupInStore && pickupPoint is not null;
            var details = new CartDetails
            {
                Placement = placement,
                Customer = customer,
                Store = store,
                Cart = cart.ToList(),
                CurrencyCode = currencyCode,
                IsPickup = pickupInStore,
                PickupPoint = pickupPoint
            };

            //recalculate the total and update the items, since the amounts may have changed
            var items = await PrepareOrderItemsAsync(details);
            var orderAmount = await PrepareOrderMoneyAsync(details, items);
            var cardData = new CardData
            {
                Level2 = new() { InvoiceId = CommonHelper.EnsureMaximumLength(paymentRequest.OrderGuid.ToString(), 127) },
                Level3 = new()
                {
                    LineItems = items,
                    ShippingAmount = orderAmount.Breakdown.Shipping,
                    ShippingAddress = unit.Shipping?.Address,
                    ShipsFromPostalCode = CommonHelper.EnsureMaximumLength((await _addressService
                        .GetAddressByIdAsync(_shippingSettings.ShippingOriginAddressId))?.ZipPostalCode, 60)
                },
            };

            var patches = PreparePatches(new()
            {
                //if the shipping option type is set to PICKUP, then the full name should start with S2S meaning ship to store (for example, S2S My Store)
                Shipping = details.IsPickup && details.PickupPoint is not null
                    ? new Shipping { Name = new() { FullName = $"S2S {details.PickupPoint.Name}" } }
                    : null,
                Items = items,
                Amount = orderAmount,
                SupplementaryData = new() { Card = cardData }
            });
            await PatchOrderWithServerSdkAsync(settings, order.Id, patches);

            //place order immediately, if the appropriate setting is enabled
            if (placement == ButtonPlacement.PaymentMethod)
                return (order, settings.SkipOrderConfirmPage);

            //or update billing details and redirect customer to the confirmation page
            if (order.Payer is not null)
            {
                var billingCountry = await _countryService.GetCountryByTwoLetterIsoCodeAsync(order.Payer.Address?.CountryCode);
                var billingState = await _stateProvinceService
                    .GetStateProvinceByAbbreviationAsync(order.Payer.Address?.AdminArea1, billingCountry?.Id);
                var billingAddress = await PrepareCustomerAddressAsync(customer, new()
                {
                    Email = order.Payer.EmailAddress ?? customer.Email,
                    FirstName = order.Payer.Name?.GivenName ?? customer.FirstName,
                    LastName = order.Payer.Name?.Surname ?? customer.LastName,
                    Address1 = order.Payer.Address?.AddressLine1,
                    Address2 = order.Payer.Address?.AddressLine2,
                    City = order.Payer.Address?.AdminArea2,
                    ZipPostalCode = order.Payer.Address?.PostalCode,
                    StateProvinceId = billingState?.Id,
                    CountryId = billingCountry?.Id
                });
                if (billingAddress.Id != customer.BillingAddressId)
                    customer.BillingAddressId = billingAddress.Id;

                if (await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart) &&
                    await _genericAttributeService.GetAttributeAsync<NopShippingOption>(customer,
                        NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id) is NopShippingOption shippingOption &&
                    !shippingOption.IsPickupInStore &&
                    order.PurchaseUnits.FirstOrDefault()?.Shipping is Shipping shipping &&
                    shipping.Address is Address shippingAddress)
                {
                    var shippingCountry = await _countryService.GetCountryByTwoLetterIsoCodeAsync(shippingAddress.CountryCode);
                    var shippingState = await _stateProvinceService
                        .GetStateProvinceByAbbreviationAsync(shippingAddress.AdminArea1, shippingCountry?.Id);
                    var newShippingAddress = await PrepareCustomerAddressAsync(customer, new()
                    {
                        Email = order.Payer.EmailAddress ?? customer.Email,
                        Address1 = shippingAddress.AddressLine1,
                        Address2 = shippingAddress.AddressLine2,
                        City = shippingAddress.AdminArea2,
                        ZipPostalCode = shippingAddress.PostalCode,
                        StateProvinceId = shippingState?.Id,
                        CountryId = shippingCountry?.Id
                    });
                    if (newShippingAddress.Id != customer.ShippingAddressId)
                        customer.ShippingAddressId = newShippingAddress.Id;
                }

                await _customerService.UpdateCustomerAsync(customer);
            }

            return (order, false);
        });
    }

    /// <summary>
    /// Place an order
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="orderId">Order id</param>
    /// <param name="liabilityShift">Liability shift</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the placed order; created order; error message if exists
    /// </returns>
    public async Task<((NopOrder NopOrder, Order Order), string Error)>
        PlaceOrderAsync(PayPalCommerceSettings settings, string orderId, string liabilityShift)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            if (!cart.Any())
                throw new NopException("Shopping cart is empty");

            var paymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync();
            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            if (paymentRequest is null ||
                !paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                !string.Equals(orderIdValue.Value, orderId, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NopException("Failed to get the PayPal order ID");
            }

            //check the order status
            var order = await GetOrderWithServerSdkAsync(settings, orderId);
            if (order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString() && order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
            {
                if (order.Status?.ToUpper() == OrderStatusType.CREATED.ToString())
                {
                    if (liabilityShift?.ToUpper() == LiabilityShiftType.NO.ToString())
                        throw new NopException($"3D Secure contingency is not resolved");

                    if (liabilityShift?.ToUpper() == LiabilityShiftType.UNKNOWN.ToString())
                        throw new NopException($"The authentication system isn't available, please retry later");
                }
                else
                    throw new NopException($"Order is in '{order.Status}' status");
            }

            if (order.PurchaseUnits.FirstOrDefault() is not PurchaseUnit unit ||
                !string.Equals(unit.CustomId, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NopException("Failed to get PayPal order info");
            }

            //totals must match
            var (cartTotal, _, _, _, _, _) = await _orderTotalCalculationService
                .GetShoppingCartTotalAsync(cart, usePaymentMethodAdditionalFee: false);
            var difference = Math.Abs(ConvertMoney(unit.Amount) - Math.Round(cartTotal ?? decimal.Zero, 2));
            if (difference > decimal.Zero)
                throw new NopException($"Shopping cart total and approved order amount differ by {difference}");

            //prevent 2 orders being placed within an X seconds time frame
            if (_orderSettings.MinimumOrderPlacementInterval > 0)
            {
                var lastOrder = (await _orderService.SearchOrdersAsync(storeId: store.Id, customerId: customer.Id, pageSize: 1)).FirstOrDefault();
                if (lastOrder is not null && (DateTime.UtcNow - lastOrder.CreatedOnUtc).TotalMinutes < _orderSettings.MinimumOrderPlacementInterval)
                    throw new NopException(await _localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval"));
            }

            var tokenId = paymentRequest.CustomValues
                .TryGetValue(PayPalCommerceDefaults.TokenIdAttributeName, out var tokenIdValue) && int.TryParse(tokenIdValue.Value, out var id)
                ? id : 0;

            paymentRequest.StoreId = store.Id;
            paymentRequest.CustomerId = customer.Id;
            paymentRequest.PaymentMethodSystemName = PayPalCommerceDefaults.SystemName;
            paymentRequest.CustomValues.Remove(PayPalCommerceDefaults.TokenIdAttributeName);

            //try to place an order
            var placeOrderResult = await _orderProcessingService.PlaceOrderAsync(paymentRequest);
            if (placeOrderResult?.PlacedOrder is not NopOrder nopOrder || placeOrderResult?.Success != true)
                throw new NopException(string.Join(',', placeOrderResult?.Errors ?? new List<string>()));

            if (await _tokenService.GetByIdAsync(tokenId) is PayPalToken token && token.CustomerId == customer.Id)
                await _genericAttributeService.SaveAttributeAsync(nopOrder, PayPalCommerceDefaults.TokenIdAttributeName, tokenId);

            //clear payment request
            await _orderProcessingService.SetProcessPaymentRequestAsync(null);

            return (nopOrder, order);
        });
    }

    /// <summary>
    /// Confirm the placed order
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="nopOrder">Placed order</param>
    /// <param name="order">Order</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the confirmed order; error message if exists
    /// </returns>
    public async Task<(Order Order, string Error)> ConfirmOrderAsync(PayPalCommerceSettings settings, NopOrder nopOrder, Order order)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            //authorize or capture previously created order if not yet completed
            if (order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
            {
                //update invoice id
                var patch = new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/invoice_id",
                    Value = nopOrder.CustomOrderNumber
                };
                await PatchOrderWithServerSdkAsync(settings, order.Id, new List<Patch<object>> { patch });

                order = settings.PaymentType switch
                {
                    Domain.PaymentType.Authorize => await AuthorizeOrderWithServerSdkAsync(settings, order.Id),
                    Domain.PaymentType.Capture => await CaptureOrderWithServerSdkAsync(settings, order.Id),
                    _ => null
                };
            }

            //check the authorization object or the capture object
            var purchaseUnit = order.PurchaseUnits.FirstOrDefault();
            var authorization = purchaseUnit.Payments?.Authorizations?.FirstOrDefault();
            var capture = purchaseUnit.Payments?.Captures?.FirstOrDefault();
            try
            {
                if (authorization is not null)
                {
                    if (authorization.Status?.ToUpper() == AuthorizationStatusType.DENIED.ToString())
                        throw new NopException("Cannot authorize funds for this authorized payment");

                    if (authorization.Status?.ToUpper() == AuthorizationStatusType.PENDING.ToString())
                    {
                        await _orderService.InsertOrderNoteAsync(new()
                        {
                            OrderId = nopOrder.Id,
                            Note = $"Authorization is in {authorization.Status} status due to {authorization.StatusDetails?.Reason}",
                            DisplayToCustomer = true,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        if (settings.PaymentType == Domain.PaymentType.Authorize && settings.ImmediatePaymentRequired)
                            throw new NopException($"Immediate payment required but authorization is {authorization.Status}");
                    }

                    if (authorization.Status?.ToUpper() == AuthorizationStatusType.CREATED.ToString())
                    {
                        if (_orderProcessingService.CanMarkOrderAsAuthorized(nopOrder))
                        {
                            nopOrder.AuthorizationTransactionId = authorization.Id;
                            nopOrder.AuthorizationTransactionResult = authorization.Status;
                            nopOrder.AuthorizationTransactionCode = authorization.ProcessorResponse?.ResponseCode;
                            await _orderProcessingService.MarkAsAuthorizedAsync(nopOrder);
                        }
                    }
                }

                if (capture is not null)
                {
                    if (capture.Status?.ToUpper() == CaptureStatusType.DECLINED.ToString())
                        throw new NopException("The funds could not be captured");

                    if (capture.Status?.ToUpper() == CaptureStatusType.FAILED.ToString())
                        throw new NopException("There was an error while capturing payment");

                    if (capture.Status?.ToUpper() == CaptureStatusType.PENDING.ToString())
                    {
                        await _orderService.InsertOrderNoteAsync(new()
                        {
                            OrderId = nopOrder.Id,
                            Note = $"Capture is in {capture.Status} status due to {capture.StatusDetails?.Reason}",
                            DisplayToCustomer = true,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        if (settings.ImmediatePaymentRequired)
                            throw new NopException($"Immediate payment required but capture is {capture.Status}");
                    }

                    if (capture.Status?.ToUpper() == CaptureStatusType.COMPLETED.ToString())
                    {
                        if (_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                        {
                            nopOrder.CaptureTransactionId = capture.Id;
                            nopOrder.CaptureTransactionResult = capture.Status;
                            await _orderProcessingService.MarkOrderAsPaidAsync(nopOrder);
                        }
                    }
                }
            }
            catch (NopException exception)
            {
                await _orderService.InsertOrderNoteAsync(new()
                {
                    OrderId = nopOrder.Id,
                    Note = $"Error: {exception.Message}",
                    DisplayToCustomer = true,
                    CreatedOnUtc = DateTime.UtcNow
                });

                throw;
            }

            //try to get saved in vault payment token
            var vaultedPaymentMethod = order.PaymentSource?.Vault;
            if (vaultedPaymentMethod?.Status?.ToUpper() == VaultStatusType.VAULTED.ToString())
            {
                await _tokenService.InsertAsync(new()
                {
                    ClientId = settings.ClientId,
                    CustomerId = nopOrder.CustomerId,
                    IsPrimaryMethod = false,
                    VaultId = vaultedPaymentMethod.Id,
                    VaultCustomerId = vaultedPaymentMethod.Customer?.Id,
                    TransactionId = order.Id,
                    Type = order.PaymentSource?.Card is not null
                        ? nameof(order.PaymentSource.Card)
                        : (order.PaymentSource?.Venmo is not null
                        ? nameof(order.PaymentSource.Venmo)
                        : (order.PaymentSource?.PayPal is not null
                        ? nameof(order.PaymentSource.PayPal)
                        : null))
                });

                if (await _tokenService.GetTokenAsync(settings.ClientId, nopOrder.CustomerId, vaultedPaymentMethod.Id) is PayPalToken token)
                    await _genericAttributeService.SaveAttributeAsync(nopOrder, PayPalCommerceDefaults.TokenIdAttributeName, token.Id);
            }

            return order;
        });
    }

    #region Alternative payment methods

    /// <summary>
    /// Get Apple Pay transaction info
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="placement">Button placement</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the Apple Pay transaction info; error message if exists
    /// </returns>
    public async Task<((OrderMoney Amount, Contact BillingAddress, Contact ShippingAddress, Shipping Shipping, string StoreName), string Error)>
        GetAppleTransactionInfoAsync(PayPalCommerceSettings settings, ButtonPlacement placement)
    {
        return await HandleFunctionAsync(async () =>
        {
            var details = await PrepareCartDetailsAsync(placement);
            var payer = await PrepareBillingDetailsAsync(settings, details);
            var billingContact = new Contact
            {
                Email = payer.EmailAddress,
                FirstName = payer.Name.GivenName,
                LastName = payer.Name.Surname,
                AddressLines = [payer.Address?.AddressLine1, payer.Address?.AddressLine2],
                City = payer.Address?.AdminArea2,
                State = payer.Address?.AdminArea1,
                Country = payer.Address?.CountryCode,
                PostalCode = payer.Address?.PostalCode
            };

            var items = await PrepareOrderItemsAsync(details);
            var orderAmount = await PrepareOrderMoneyAsync(details, items);

            var shipping = await PrepareShippingDetailsAsync(details, details.ShippingOption?.Name, true);
            var shippingContact = new Contact
            {
                FirstName = details.ShippingAddress is not null && !details.IsPickup
                    ? details.ShippingAddress.FirstName
                    : details.Customer.FirstName,
                LastName = details.ShippingAddress is not null && !details.IsPickup
                    ? details.ShippingAddress.LastName
                    : details.Customer.LastName,
                AddressLines = [shipping.Address?.AddressLine1, shipping.Address?.AddressLine2],
                City = shipping.Address?.AdminArea2,
                State = shipping.Address?.AdminArea1,
                Country = shipping.Address?.CountryCode,
                PostalCode = shipping.Address?.PostalCode,
                PickupInStore = details.IsPickup
            };

            return (orderAmount, billingContact, shippingContact, shipping, details.Store.Name);
        });
    }

    /// <summary>
    /// Update Apple Pay shipping details
    /// </summary>
    /// <param name="placement">Button placement</param>
    /// <param name="selectedAddress">Selected shipping address</param>
    /// <param name="selectedOption">Selected shipping option</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the updated shipping details; error message if exists
    /// </returns>
    public async Task<(Shipping Shipping, string Error)> UpdateAppleShippingAsync(ButtonPlacement placement,
        (string City, string State, string Country, string PostalCode) selectedAddress, string selectedOption)
    {
        return await HandleFunctionAsync(async () =>
        {
            //changing shipping address or option on the payment method page is not available
            if (placement == ButtonPlacement.PaymentMethod)
                return null;

            var details = await PrepareCartDetailsAsync(placement, false);
            if (!details.ShippingIsRequired)
                return null;

            //get option id
            var optionValues = selectedOption?.Split('|', StringSplitOptions.RemoveEmptyEntries)?.ToList() ?? new();
            var option = (optionValues.FirstOrDefault(), optionValues.LastOrDefault());
            var shipping = await PrepareUpdatedShippingAsync(details, details.Customer.Email, selectedAddress, option);

            return shipping;
        });
    }

    /// <summary>
    /// Get Google Pay transaction info
    /// </summary>
    /// <param name="placement">Button placement</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the Google Pay transaction info; error message if exists
    /// </returns>
    public async Task<((OrderMoney Amount, string Country, bool ShippingIsRequired), string Error)>
        GetGoogleTransactionInfoAsync(ButtonPlacement placement)
    {
        return await HandleFunctionAsync(async () =>
        {
            var details = await PrepareCartDetailsAsync(placement);
            var items = await PrepareOrderItemsAsync(details);
            var orderAmount = await PrepareOrderMoneyAsync(details, items);

            var country = await _countryService.GetCountryByIdAsync(details.BillingAddress?.CountryId ?? details.Customer.CountryId);

            return (orderAmount, country?.TwoLetterIsoCode ?? "US", details.ShippingIsRequired);
        });
    }

    /// <summary>
    /// Update Google Pay shipping details
    /// </summary>
    /// <param name="placement">Button placement</param>
    /// <param name="selectedAddress">Selected shipping address</param>
    /// <param name="selectedOption">Selected shipping option</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the updated shipping details; error message if exists
    /// </returns>
    public async Task<(Shipping Shipping, string Error)> UpdateGoogleShippingAsync(ButtonPlacement placement,
        (string City, string State, string Country, string PostalCode) selectedAddress, string selectedOption)
    {
        return await HandleFunctionAsync(async () =>
        {
            //changing shipping address or option on the payment method page is not available
            if (placement == ButtonPlacement.PaymentMethod)
                return null;

            var details = await PrepareCartDetailsAsync(placement, false);
            if (!details.ShippingIsRequired)
                return null;

            //get option id
            var optionValues = selectedOption?.Split('|', StringSplitOptions.RemoveEmptyEntries)?.ToList() ?? new();
            var option = (optionValues.FirstOrDefault(), optionValues.LastOrDefault());
            var shipping = await PrepareUpdatedShippingAsync(details, details.Customer.Email, selectedAddress, option);

            return shipping;
        });
    }

    #endregion

    #endregion

    #region Payments

    /// <summary>
    /// Capture an authorization
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="authorizationId">Authorization id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the capture details; error message if exists
    /// </returns>
    public async Task<(Capture Capture, string Error)> CaptureAuthorizationAsync(PayPalCommerceSettings settings, string authorizationId)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            if (string.IsNullOrEmpty(authorizationId))
                throw new NopException("Authorization ID not set");

            var input = new SdkModels.CaptureAuthorizedPaymentInput(
                authorizationId: authorizationId,
                contentType: "application/json",
                paypalRequestId: Guid.NewGuid().ToString(),
                prefer: "return=representation");

            var response = await _paypalServerSdkClient.PaymentsController.CaptureAuthorizedPaymentAsync(input);
            var sdkCapture = response?.Data;
            if (sdkCapture is null)
                throw new NopException("Failed to read PayPal capture data.");

            var status = sdkCapture.Status?.ToString()?.ToUpperInvariant();

            if (status == CaptureStatusType.DECLINED.ToString())
                throw new NopException("The funds could not be captured");

            if (status == CaptureStatusType.FAILED.ToString())
                throw new NopException("There was an error while capturing payment");

            if (status == CaptureStatusType.PENDING.ToString())
                throw new NopException($"Capture is in {sdkCapture.Status} status due to {sdkCapture.StatusDetails?.Reason}");

            // Map SDK captured payment back to existing Capture model via JSON round-trip
            var serializedCapture = JsonConvert.SerializeObject(sdkCapture);
            var capture = JsonConvert.DeserializeObject<Capture>(serializedCapture);

            return capture;
        });
    }

    /// <summary>
    /// Void an authorization
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="authorizationId">Authorization id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the void result; error message if exists
    /// </returns>
    public async Task<(bool Result, string Error)> VoidAsync(PayPalCommerceSettings settings, string authorizationId)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            if (string.IsNullOrEmpty(authorizationId))
                throw new NopException("Authorization ID not set");

            var input = new SdkModels.VoidPaymentInput
            {
                AuthorizationId = authorizationId,
                PaypalRequestId = Guid.NewGuid().ToString(),
                Prefer = "return=representation"
            };

            await _paypalServerSdkClient.PaymentsController.VoidPaymentAsync(input);

            return true;
        });
    }

    /// <summary>
    /// Refund a captured payment
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="nopOrder">Order</param>
    /// <param name="amount">Amount to refund; pass null to refund the full captured amount</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the refund details; error message if exists
    /// </returns>
    public async Task<(Refund Refund, string Error)> RefundAsync(PayPalCommerceSettings settings, NopOrder nopOrder, decimal? amount = null)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
                throw new NopException("Primary store currency not set");

            if (string.IsNullOrEmpty(nopOrder.CaptureTransactionId))
                throw new NopException("Capture ID not set");

            var refundInput = new SdkModels.RefundCapturedPaymentInput
            {
                CaptureId = nopOrder.CaptureTransactionId,
                PaypalRequestId = Guid.NewGuid().ToString(),
                Prefer = "return=representation"
            };

            if (amount.HasValue)
            {
                refundInput.Body = new SdkModels.RefundRequest
                {
                    Amount = PrepareSdkMoney(amount.Value, currencyCode)
                };
            }

            var refundResponse = await _paypalServerSdkClient.PaymentsController.RefundCapturedPaymentAsync(refundInput);
            var sdkRefund = refundResponse?.Data;
            if (sdkRefund is null)
                throw new NopException("Failed to read PayPal refund data.");

            // Map SDK refund back to existing Refund model via JSON round-trip
            var serializedRefund = JsonConvert.SerializeObject(sdkRefund);
            var refund = JsonConvert.DeserializeObject<Refund>(serializedRefund);

            if (refund.Status?.ToUpper() == RefundStatusType.CANCELLED.ToString())
                throw new NopException("The refund was cancelled");

            if (refund.Status?.ToUpper() == RefundStatusType.FAILED.ToString())
                throw new NopException("The refund could not be processed");

            if (refund.Status?.ToUpper() == RefundStatusType.PENDING.ToString())
                throw new NopException($"Capture is in {refund.Status} status due to {refund.StatusDetails?.Reason}");

            //save id to avoid double refund
            var refundIds = await _genericAttributeService
                .GetAttributeAsync<List<string>>(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName)
                ?? new();
            if (!refundIds.Contains(refund.Id))
                refundIds.Add(refund.Id);
            await _genericAttributeService.SaveAttributeAsync(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName, refundIds);

            return refund;
        });
    }

    #endregion

    #region Recurring payments

    /// <summary>
    /// Create setup token to allow the payer authenticate and approve the creation of a billing agreement
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the setup token; error message if exists
    /// </returns>
    public async Task<(PaymentToken PaymentToken, string Error)> CreateSetupTokenAsync(PayPalCommerceSettings settings)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            if (!settings.UseVault)
                throw new NopException("Vault disabled");

            var details = await PrepareCartDetailsAsync(ButtonPlacement.PaymentMethod);

            if (!await _shoppingCartService.ShoppingCartIsRecurringAsync(details.Cart))
                throw new NopException("Shopping cart has no recurring items");

            if (await _customerService.IsGuestAsync(details.Customer))
                throw new NopException("Anonymous checkout disabled for recurring items");

            var (error, cycleLength, cyclePeriod, totalCycles) = await _shoppingCartService.GetRecurringCycleInfoAsync(details.Cart);
            if (!string.IsNullOrEmpty(error))
                throw new NopException(error);

            var orderGuid = Guid.NewGuid().ToString();
            var items = await PrepareOrderItemsAsync(details);
            var orderAmount = await PrepareOrderMoneyAsync(details, items);
            var shipping = await PrepareShippingDetailsAsync(details, details.ShippingOption?.Name);

            var context = PrepareRecurringPaymentContext(details);
            var payer = await PrepareBillingDetailsAsync(settings, details);

            //prepare subscription (billing plan) details
            var billingCycle = new BillingCycle
            {
                Sequence = 1,
                TenureType = TenureType.REGULAR.ToString().ToUpper(),
                TotalCycles = totalCycles,
                PricingScheme = new()
                {
                    PricingModel = PricingModelType.FIXED.ToString().ToUpper(),
                    Price = orderAmount
                },
                Frequency = new()
                {
                    IntervalCount = cycleLength,
                    IntervalUnit = cyclePeriod switch
                    {
                        RecurringProductCyclePeriod.Days => IntervalUnitType.DAY.ToString().ToUpper(),
                        RecurringProductCyclePeriod.Weeks => IntervalUnitType.WEEK.ToString().ToUpper(),
                        RecurringProductCyclePeriod.Months => IntervalUnitType.MONTH.ToString().ToUpper(),
                        RecurringProductCyclePeriod.Years => IntervalUnitType.YEAR.ToString().ToUpper(),
                        _ => null,
                    }
                }
            };

            var request = new CreateSetupTokenRequest
            {
                Customer = payer,
                PaymentSource = new()
                {
                    PayPal = new()
                    {
                        Description = CommonHelper.EnsureMaximumLength($"Purchase at '{details.Store.Name}'", 127),
                        PermitMultiplePaymentTokens = false,
                        CustomerType = VaultUsageType.CONSUMER.ToString().ToUpper(),
                        UsageType = VaultUsageType.MERCHANT.ToString().ToUpper(),
                        UsagePattern = UsagePatternType.INSTALLMENT_PREPAID.ToString().ToUpper(),
                        BillingPlan = new()
                        {
                            BillingCycles = new() { billingCycle },
                            OneTimeCharges = new() { TotalAmount = orderAmount }
                        },
                        Shipping = shipping,
                        ExperienceContext = context
                    }
                }
            };

            // map plugin request to Server SDK model and create setup token via SDK
            var sdkRequestJson = JsonConvert.SerializeObject(request);
            var sdkRequest = JsonConvert.DeserializeObject<SdkModels.SetupTokenRequest>(sdkRequestJson);

            var createSetupTokenInput = new SdkModels.CreateSetupTokenInput
            {
                Body = sdkRequest
            };

            var createSetupTokenResponse = await _paypalServerSdkClient.VaultController.CreateSetupTokenAsync(createSetupTokenInput);
            var sdkSetupToken = createSetupTokenResponse?.Data;

            return MapSetupTokenFromServerSdk(sdkSetupToken);
        });
    }

    /// <summary>
    /// Create a recurring order
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="setupTokenId">Setup token id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the created order; error message if exists
    /// </returns>
    public async Task<(Order Order, string Error)> CreateRecurringOrderAsync(PayPalCommerceSettings settings, string setupTokenId)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            if (string.IsNullOrEmpty(settings.MerchantId))
                throw new NopException("Merchant PayPal ID not set");

            if (!settings.UseVault)
                throw new NopException("Vault disabled");

            if (string.IsNullOrEmpty(setupTokenId))
                throw new NopException("Setup token not set");

            var details = await PrepareCartDetailsAsync(ButtonPlacement.PaymentMethod);

            if (!await _shoppingCartService.ShoppingCartIsRecurringAsync(details.Cart))
                throw new NopException("Shopping cart has no recurring items");

            if (await _customerService.IsGuestAsync(details.Customer))
                throw new NopException("Anonymous checkout disabled for recurring items");

            //try to create payment token by the approved setup token
            var payer = await PrepareBillingDetailsAsync(settings, details);
            var paymentTokenRequest = new CreatePaymentTokenRequest
            {
                Customer = payer,
                PaymentSource = new()
                {
                    Token = new()
                    {
                        Id = setupTokenId,
                        Type = PaymentTokenType.SETUP_TOKEN.ToString().ToUpper()
                    }
                }
            };

            // map plugin request to Server SDK model and create payment token via SDK
            var paymentTokenRequestJson = JsonConvert.SerializeObject(paymentTokenRequest);
            var paymentTokenSdkRequest = JsonConvert.DeserializeObject<SdkModels.PaymentTokenRequest>(paymentTokenRequestJson);

            var createPaymentTokenInput = new SdkModels.CreatePaymentTokenInput
            {
                Body = paymentTokenSdkRequest
            };

            var createPaymentTokenResponse = await _paypalServerSdkClient.VaultController.CreatePaymentTokenAsync(createPaymentTokenInput);
            var paymentToken = MapPaymentTokenFromServerSdk(createPaymentTokenResponse?.Data);
            if (string.IsNullOrEmpty(paymentToken?.Id))
                throw new NopException("Payment token not created");

            await _tokenService.InsertAsync(new()
            {
                ClientId = settings.ClientId,
                CustomerId = details.Customer.Id,
                IsPrimaryMethod = false,
                VaultId = paymentToken.Id,
                VaultCustomerId = paymentToken.Customer?.Id,
                TransactionId = paymentToken.Metadata?.OrderId,
                Type = paymentToken.PaymentSource?.Card is not null
                    ? nameof(paymentToken.PaymentSource.Card)
                    : (paymentToken.PaymentSource?.PayPal is not null
                    ? nameof(paymentToken.PaymentSource.PayPal)
                    : null)
            });

            var paymentRequest = new ProcessPaymentRequest();

            //prepare purchase unit
            var purchaseUnit = await PreparePurchaseUnitAsync(settings, details, paymentRequest.OrderGuid.ToString());

            //set payment source
            var paymentSourceDetails = new PaymentSource();
            if (paymentToken.PaymentSource?.PayPal is not null)
            {
                paymentSourceDetails.PayPal = new()
                {
                    EmailAddress = payer.EmailAddress,
                    VaultId = paymentToken.Id,
                    StoredCredential = new()
                    {
                        PaymentInitiator = PaymentInitiatorType.CUSTOMER.ToString().ToUpper(),
                        Usage = StoredPaymentUsageType.SUBSEQUENT.ToString().ToUpper(),
                        UsagePattern = UsagePatternType.INSTALLMENT_PREPAID.ToString().ToUpper(),
                    }
                };
            }

            var intent = MapCheckoutPaymentIntent(settings.PaymentType);

            var purchaseUnits = new List<SdkModels.PurchaseUnitRequest>();

            if (purchaseUnit is not null)
            {
                var sdkPurchaseUnit = new SdkModels.PurchaseUnitRequest(
                    amount: MapAmountWithBreakdownToServerSdk(purchaseUnit.Amount),
                    referenceId: purchaseUnit.ReferenceId,
                    payee: MapPayeeToServerSdk(purchaseUnit.Payee),
                    paymentInstruction: MapPaymentInstructionToServerSdk(purchaseUnit.PaymentInstruction),
                    description: purchaseUnit.Description,
                    customId: purchaseUnit.CustomId,
                    invoiceId: purchaseUnit.InvoiceId,
                    softDescriptor: purchaseUnit.SoftDescriptor,
                    items: purchaseUnit.Items?.Select(MapItemToServerSdk).Where(item => item is not null).ToList(),
                    shipping: MapShippingToServerSdk(purchaseUnit.Shipping),
                    supplementaryData: MapSupplementaryDataToServerSdk(purchaseUnit.SupplementaryData));

                if (sdkPurchaseUnit is not null)
                    purchaseUnits.Add(sdkPurchaseUnit);
            }

            SdkModels.PaymentSource sdkPaymentSource = null;
            if (paymentSourceDetails is not null)
            {
                sdkPaymentSource = new SdkModels.PaymentSource();

                if (paymentSourceDetails.Card is not null)
                    sdkPaymentSource.Card = MapCardToServerSdk(paymentSourceDetails.Card);

                if (paymentSourceDetails.PayPal is not null)
                    sdkPaymentSource.Paypal = MapPaypalWalletToServerSdk(paymentSourceDetails.PayPal);

                if (paymentSourceDetails.Venmo is not null)
                    sdkPaymentSource.Venmo = MapVenmoWalletToServerSdk(paymentSourceDetails.Venmo);
            }

            var orderRequest = new SdkModels.OrderRequest(
                intent: intent,
                purchaseUnits: purchaseUnits,
                payer: null,
                paymentSource: sdkPaymentSource,
                applicationContext: null);

            var order = await CreateOrderWithServerSdkAsync(settings, orderRequest);

            //save order details for future using as the payment request
            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            paymentRequest.CustomValues[orderIdKey] = order.Id;

            var placementKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Placement");
            paymentRequest.CustomValues.Remove(placementKey);
            paymentRequest.CustomValues.Add(new(placementKey, ButtonPlacement.PaymentMethod.ToString(), displayToCustomer: false));

            if (await _tokenService.GetTokenAsync(settings.ClientId, details.Customer.Id, paymentToken.Id) is PayPalToken token)
            {
                paymentRequest.CustomValues.Remove(PayPalCommerceDefaults.TokenIdAttributeName);
                paymentRequest.CustomValues.Add(new(PayPalCommerceDefaults.TokenIdAttributeName, token.Id.ToString(), displayToCustomer: false));
            }

            await _orderProcessingService.SetProcessPaymentRequestAsync(paymentRequest, true);

            return order;
        });
    }

    /// <summary>
    /// Process next recurring payment
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="paymentRequest">Payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the created order; error message if exists
    /// </returns>
    public async Task<(Order Order, string Error)> ProcessNextRecurringPaymentAsync(PayPalCommerceSettings settings,
        ProcessPaymentRequest paymentRequest)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var tokenId = await _genericAttributeService
                .GetAttributeAsync<int>(paymentRequest.InitialOrder, PayPalCommerceDefaults.TokenIdAttributeName);
            if (await _tokenService.GetByIdAsync(tokenId) is not PayPalToken token || token.CustomerId != paymentRequest.CustomerId)
                throw new NopException("Payment token not found");

            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
                throw new NopException("Primary store currency not set");

            //prepare purchase unit
            var store = await _storeService.GetStoreByIdAsync(paymentRequest.StoreId);
            var orderGuid = paymentRequest.OrderGuid.ToString();
            var money = PrepareMoney(paymentRequest.OrderTotal, currencyCode);
            var purchaseUnit = new PurchaseUnit
            {
                CustomId = CommonHelper.EnsureMaximumLength(orderGuid, 127),
                InvoiceId = CommonHelper.EnsureMaximumLength(orderGuid, 127),
                Description = CommonHelper.EnsureMaximumLength($"Purchase at '{store.Name}'", 127),
                SoftDescriptor = CommonHelper.EnsureMaximumLength(store.Name, 22),
                Payee = new() { MerchantId = settings.MerchantId },
                Amount = new() { Value = money.Value, CurrencyCode = money.CurrencyCode }
            };

            //set payment source
            var paymentSourceDetails = new PaymentSource();
            var storedCredential = new StoredCredential
            {
                PaymentInitiator = PaymentInitiatorType.MERCHANT.ToString().ToUpper(),
                Usage = StoredPaymentUsageType.SUBSEQUENT.ToString().ToUpper(),
                UsagePattern = UsagePatternType.INSTALLMENT_PREPAID.ToString().ToUpper(),
                PaymentType = Api.Models.Enums.PaymentType.RECURRING.ToString().ToUpper()
            };
            if (string.Equals(token.Type, nameof(PaymentSource.Card), StringComparison.InvariantCultureIgnoreCase))
                paymentSourceDetails.Card = new() { VaultId = token.VaultId, StoredCredential = storedCredential };
            else if (string.Equals(token.Type, nameof(PaymentSource.PayPal), StringComparison.InvariantCultureIgnoreCase))
                paymentSourceDetails.PayPal = new() { VaultId = token.VaultId, StoredCredential = storedCredential };

            var intent = MapCheckoutPaymentIntent(settings.PaymentType);

            var purchaseUnits = new List<SdkModels.PurchaseUnitRequest>();

            if (purchaseUnit is not null)
            {
                var sdkPurchaseUnit = new SdkModels.PurchaseUnitRequest(
                    amount: MapAmountWithBreakdownToServerSdk(purchaseUnit.Amount),
                    referenceId: purchaseUnit.ReferenceId,
                    payee: MapPayeeToServerSdk(purchaseUnit.Payee),
                    paymentInstruction: MapPaymentInstructionToServerSdk(purchaseUnit.PaymentInstruction),
                    description: purchaseUnit.Description,
                    customId: purchaseUnit.CustomId,
                    invoiceId: purchaseUnit.InvoiceId,
                    softDescriptor: purchaseUnit.SoftDescriptor,
                    items: purchaseUnit.Items?.Select(MapItemToServerSdk).Where(item => item is not null).ToList(),
                    shipping: MapShippingToServerSdk(purchaseUnit.Shipping),
                    supplementaryData: MapSupplementaryDataToServerSdk(purchaseUnit.SupplementaryData));

                if (sdkPurchaseUnit is not null)
                    purchaseUnits.Add(sdkPurchaseUnit);
            }

            SdkModels.PaymentSource sdkPaymentSource = null;
            if (paymentSourceDetails is not null)
            {
                sdkPaymentSource = new SdkModels.PaymentSource();

                if (paymentSourceDetails.Card is not null)
                    sdkPaymentSource.Card = MapCardToServerSdk(paymentSourceDetails.Card);

                if (paymentSourceDetails.PayPal is not null)
                    sdkPaymentSource.Paypal = MapPaypalWalletToServerSdk(paymentSourceDetails.PayPal);

                if (paymentSourceDetails.Venmo is not null)
                    sdkPaymentSource.Venmo = MapVenmoWalletToServerSdk(paymentSourceDetails.Venmo);
            }

            var orderRequest = new SdkModels.OrderRequest(
                intent: intent,
                purchaseUnits: purchaseUnits,
                payer: null,
                paymentSource: sdkPaymentSource,
                applicationContext: null);

            var order = await CreateOrderWithServerSdkAsync(settings, orderRequest);

            return order;
        });
    }

    /// <summary>
    /// Cancel recurring payment
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="initialOrder">Order</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the cancel result; error message if exists
    /// </returns>
    public async Task<(bool Result, string Error)> CancelRecurringPaymentAsync(PayPalCommerceSettings settings, NopOrder initialOrder)
    {
        return await HandleFunctionAsync(() =>
        {
            //no special action required
            //we don't delete the payment token because the customer can use it for other orders

            return Task.FromResult(true);
        });
    }

    #endregion

    #region Shipping

    /// <summary>
    /// Add package tracking number to an order
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="shipment">Shipment</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the operation result; error message if exists
    /// </returns>
    public async Task<(bool Result, string Error)> SetTrackingAsync(PayPalCommerceSettings settings, Shipment shipment)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var carrier = await _genericAttributeService.GetAttributeAsync<string>(shipment, PayPalCommerceDefaults.ShipmentCarrierAttribute);
            if (string.IsNullOrEmpty(carrier))
                return false;

            var nopOrder = await _orderService.GetOrderByIdAsync(shipment?.OrderId ?? 0)
                ?? throw new NopException("Order cannot be loaded");

            if (!string.Equals(nopOrder.PaymentMethodSystemName, PayPalCommerceDefaults.SystemName, StringComparison.InvariantCultureIgnoreCase))
                return false;

            var customValues = new CustomValues();
            customValues.FillByXml(nopOrder.CustomValuesXml);
            var orderIdKey = await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Order.Id");
            if (!customValues.TryGetValue(orderIdKey, out var orderIdValue))
                throw new NopException("Failed to get PayPal order info");

            var order = await GetOrderWithServerSdkAsync(settings, orderIdValue.Value);
            if (order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
                throw new NopException($"Unable to assign tracking information to orders in {order.Status} status");

            if (order.PurchaseUnits?.FirstOrDefault() is not PurchaseUnit unit || unit.Shipping is null)
                throw new NopException("No shipping info found for PayPal order");

            if (unit.Payments?.Captures?.FirstOrDefault() is not Capture capture ||
                (capture.Status?.ToUpper() != CaptureStatusType.COMPLETED.ToString() &&
                capture.Status?.ToUpper() != CaptureStatusType.PARTIALLY_REFUNDED.ToString() &&
                capture.Status?.ToUpper() != CaptureStatusType.PENDING.ToString()))
            {
                throw new NopException($"Unable to assign tracking information to orders with the payment in {order.Status} status");
            }

            var shipmentItems = await _shipmentService.GetShipmentItemsByShipmentIdAsync(shipment.Id);
            var items = await shipmentItems.SelectAwait(async shipmentItem =>
            {
                var orderItem = await _orderService.GetOrderItemByIdAsync(shipmentItem.OrderItemId);
                var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
                var sku = await _productService.FormatSkuAsync(product, orderItem.AttributesXml);
                var url = await _nopUrlHelper.RouteGenericUrlAsync(product, _webHelper.GetCurrentRequestProtocol());
                var picture = await _pictureService.GetProductPictureAsync(product, orderItem.AttributesXml);
                var (imageUrl, _) = await _pictureService.GetPictureUrlAsync(picture);

                return new Item
                {
                    Name = CommonHelper.EnsureMaximumLength(product.Name, 127),
                    Description = CommonHelper.EnsureMaximumLength(product.ShortDescription, 127),
                    Sku = CommonHelper.EnsureMaximumLength(sku, 127),
                    Quantity = shipmentItem.Quantity.ToString(),
                    Url = url,
                    ImageUrl = imageUrl
                };
            }).ToListAsync();

            var request = new CreateTrackingRequest
            {
                OrderId = order.Id,
                CaptureId = capture.Id,
                TrackingNumber = shipment.TrackingNumber,
                NotifyPayer = true,
                Carrier = carrier,
                Items = items
            };
            order = await _httpClient.RequestAsync<CreateTrackingRequest, CreateTrackingResponse>(request, settings);

            return true;
        });
    }

    #endregion

    #region Webhooks

    /// <summary>
    /// Get webhook by the URL
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="webhookUrl">Webhook URL</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the webhook; error message if exists
    /// </returns>
    public async Task<(Webhook Webhook, string Error)> GetWebhookAsync(PayPalCommerceSettings settings, string webhookUrl)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            var webhookList = await _httpClient.RequestAsync<GetWebhooksRequest, GetWebhooksResponse>(new(), settings);
            var webhookByUrl = webhookList?.Webhooks
                ?.FirstOrDefault(webhook => webhook.Url?.Equals(webhookUrl, StringComparison.InvariantCultureIgnoreCase) ?? false);

            return webhookByUrl;
        });
    }

    /// <summary>
    /// Create webhook that receive events for the subscribed event types
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="storeId">Store id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the webhook; error message if exists
    /// </returns>
    public async Task<(Webhook Webhook, string Error)> CreateWebhookAsync(PayPalCommerceSettings settings, int storeId)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (!IsConfigured(settings))
                throw new NopException("Plugin not configured");

            //prepare webhook URL
            var store = storeId > 0
                ? await _storeService.GetStoreByIdAsync(storeId)
                : await _storeContext.GetCurrentStoreAsync();
            var webhookUrl = $"{store.Url.TrimEnd('/')}{_nopUrlHelper.RouteUrl(PayPalCommerceDefaults.Route.Webhook)}".ToLowerInvariant();

            //check whether the webhook already exists
            var (webhook, _) = await GetWebhookAsync(settings, webhookUrl);
            if (webhook is not null)
                return webhook;

            //or try to create a new one
            var request = new CreateWebhookRequest
            {
                EventTypes = PayPalCommerceDefaults.WebhookEventNames.Select(name => new EventType { Name = name }).ToList(),
                Url = webhookUrl
            };
            var result = await _httpClient.RequestAsync<CreateWebhookRequest, CreateWebhookResponse>(request, settings);

            return result;
        });
    }

    /// <summary>
    /// Delete webhook
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeleteWebhookAsync(PayPalCommerceSettings settings)
    {
        await HandleFunctionAsync(async () =>
        {
            if (!IsConnected(settings))
                throw new NopException("Plugin not connected");

            var (webhook, _) = await GetWebhookAsync(settings, settings.WebhookUrl);
            if (webhook is not null)
                await _httpClient.RequestAsync<DeleteWebhookRequest, EmptyResponse>(new() { WebhookId = webhook.Id }, settings);

            return true;
        });
    }

    /// <summary>
    /// Handle webhook request
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="request">HTTP request</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleWebhookAsync(PayPalCommerceSettings settings, Microsoft.AspNetCore.Http.HttpRequest request)
    {
        await HandleFunctionAsync(async () =>
        {
            //ensure that plugin is configured and connected
            if (!IsConnected(settings))
                throw new NopException("Webhook error", new NopException("Plugin not connected"));

            //get request content
            var webhookEvent = string.Empty;
            using (var streamReader = new StreamReader(request.Body))
                webhookEvent = await streamReader.ReadToEndAsync();

            var (webhook, _) = await GetWebhookAsync(settings, settings.WebhookUrl);
            if (webhook is null)
                throw new NopException("Webhook error", new NopException($"No webhook configured for URL '{settings.WebhookUrl}'"));

            //define a local function to validate the webhook event and get its resource
            async Task<IWebhookResource> getWebhookResource<TResource>() where TResource : class, IWebhookResource
            {
                //verify webhook event data
                var verifyRequest = new CreateWebhookSignatureRequest
                {
                    AuthAlgo = request.Headers["PAYPAL-AUTH-ALGO"],
                    CertUrl = request.Headers["PAYPAL-CERT-URL"],
                    TransmissionId = request.Headers["PAYPAL-TRANSMISSION-ID"],
                    TransmissionSig = request.Headers["PAYPAL-TRANSMISSION-SIG"],
                    TransmissionTime = request.Headers["PAYPAL-TRANSMISSION-TIME"],
                    WebhookId = webhook.Id,
                    WebhookEvent = new(webhookEvent)
                };
                var result = await _httpClient.RequestAsync<CreateWebhookSignatureRequest, CreateWebhookSignatureResponse>(verifyRequest, settings);

                if (result?.VerificationStatus?.ToUpper() != WebhookSignatureVerificationStatusType.SUCCESS.ToString())
                    throw new NopException("Webhook error", new NopException($"Webhook signature verification {result?.VerificationStatus}"));

                return JsonConvert.DeserializeObject<Event<TResource>>(webhookEvent)?.Resource;
            }

            //try to get webhook resource
            var webhookResourceType = JsonConvert.DeserializeObject<Event<WebhookResource>>(webhookEvent)?.ResourceType;
            var webhookResource = webhookResourceType switch
            {
                var type when string.Equals(type, nameof(Authorization), StringComparison.InvariantCultureIgnoreCase)
                    => await getWebhookResource<Authorization>(),
                var type when string.Equals(type, nameof(Capture), StringComparison.InvariantCultureIgnoreCase)
                    => await getWebhookResource<Capture>(),
                var type when string.Equals(type, nameof(Refund), StringComparison.InvariantCultureIgnoreCase)
                    => await getWebhookResource<Refund>(),
                var type when string.Equals(type?.Replace("checkout-", string.Empty), nameof(Order), StringComparison.InvariantCultureIgnoreCase)
                    => await getWebhookResource<Order>(),
                var type when string.Equals(type?.Replace("_", string.Empty), nameof(PaymentToken), StringComparison.InvariantCultureIgnoreCase)
                    => await getWebhookResource<PaymentToken>(),
                _ => null
            } ?? throw new NopException("Webhook error", new NopException($"Unknown webhook resource type '{webhookResourceType}'"));

            var paymentToken = webhookResource as PaymentToken;
            if (paymentToken is not null)
            {
                //payment token actions
                var eventType = JsonConvert.DeserializeObject<Event<WebhookResource>>(webhookEvent)?.EventType;
                var paymentTokenCreated = string.Equals(eventType, "VAULT.PAYMENT-TOKEN.CREATED", StringComparison.InvariantCultureIgnoreCase);
                var paymentTokenDeleted =
                    string.Equals(eventType, "VAULT.PAYMENT-TOKEN.DELETION-INITIATED", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(eventType, "VAULT.PAYMENT-TOKEN.DELETED", StringComparison.InvariantCultureIgnoreCase);

                if (paymentTokenCreated)
                {
                    var customerId = int.TryParse(paymentToken.Customer?.MerchantCustomerId, out var id) ? id : (int?)null;

                    //try to get associated transaction
                    if (!string.IsNullOrEmpty(paymentToken.Metadata?.OrderId))
                    {
                        try
                        {
                            var paymentTokenOrder = await GetOrderWithServerSdkAsync(settings, paymentToken.Metadata.OrderId);
                            if (Guid.TryParse(paymentTokenOrder.CustomId, out var guid))
                                customerId = (await _orderService.GetOrderByGuidAsync(guid))?.CustomerId;
                        }
                        catch { }
                    }

                    await _tokenService.InsertAsync(new()
                    {
                        ClientId = settings.ClientId,
                        CustomerId = customerId ?? 0,
                        IsPrimaryMethod = false,
                        VaultId = paymentToken.Id,
                        VaultCustomerId = paymentToken.Customer?.Id,
                        TransactionId = paymentToken.Metadata?.OrderId,
                        Type = paymentToken.PaymentSource?.Card is not null
                            ? nameof(paymentToken.PaymentSource.Card)
                            : (paymentToken.PaymentSource?.Venmo is not null
                            ? nameof(paymentToken.PaymentSource.Venmo)
                            : (paymentToken.PaymentSource?.PayPal is not null
                            ? nameof(paymentToken.PaymentSource.PayPal)
                            : null))
                    });

                    return true;
                }

                if (paymentTokenDeleted)
                {
                    var tokens = await _tokenService.GetAllTokensAsync(settings.ClientId, vaultId: paymentToken.Id);
                    if (tokens.Any())
                        await _tokenService.DeleteAsync(tokens);

                    return true;
                }
            }

            if (!Guid.TryParse(webhookResource.CustomId, out var orderGuid) ||
                await _orderService.GetOrderByGuidAsync(orderGuid) is not NopOrder nopOrder)
            {
                throw new NopException("Webhook error", new NopException($"Could not find an order '{orderGuid}'"));
            }

            await _orderService.InsertOrderNoteAsync(new()
            {
                OrderId = nopOrder.Id,
                Note = $"Webhook details: {Environment.NewLine}{Newtonsoft.Json.Linq.JToken.Parse(webhookEvent).ToString(Formatting.Indented)}",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            //authorization actions
            var authorization = webhookResource as Authorization;
            if (authorization is not null && Enum.TryParse<AuthorizationStatusType>(authorization.Status, true, out var authorizationStatus))
            {
                nopOrder.AuthorizationTransactionId = authorization.Id;
                nopOrder.AuthorizationTransactionResult = authorization.Status;

                switch (authorizationStatus)
                {
                    case AuthorizationStatusType.PENDING:
                        nopOrder.PaymentStatus = PaymentStatus.Pending;
                        await _orderProcessingService.CheckOrderStatusAsync(nopOrder);

                        break;

                    case AuthorizationStatusType.VOIDED:
                        if (_orderProcessingService.CanVoidOffline(nopOrder))
                            await _orderProcessingService.VoidOfflineAsync(nopOrder);

                        break;

                    case AuthorizationStatusType.CREATED:
                        if (!_orderProcessingService.CanMarkOrderAsAuthorized(nopOrder))
                            break;

                        if (ConvertMoney(authorization.Amount) >= Math.Round(nopOrder.OrderTotal, 2))
                            await _orderProcessingService.MarkAsAuthorizedAsync(nopOrder);

                        break;

                    case AuthorizationStatusType.DENIED:
                        await _orderService.InsertOrderNoteAsync(new()
                        {
                            OrderId = nopOrder.Id,
                            Note = "Cannot authorize funds for this authorized payment",
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        break;

                    case AuthorizationStatusType.CAPTURED:
                    case AuthorizationStatusType.PARTIALLY_CAPTURED:

                        //processed by the capture object

                        break;
                }
            }

            //capture actions
            var capture = webhookResource as Capture;
            if (capture is not null && Enum.TryParse<CaptureStatusType>(capture.Status, true, out var captureStatus))
            {
                nopOrder.CaptureTransactionId = capture.Id;
                nopOrder.CaptureTransactionResult = capture.Status;

                switch (captureStatus)
                {
                    case CaptureStatusType.PENDING:
                        nopOrder.PaymentStatus = PaymentStatus.Pending;
                        await _orderProcessingService.CheckOrderStatusAsync(nopOrder);

                        break;

                    case CaptureStatusType.COMPLETED:
                        if (!_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                            break;

                        if (ConvertMoney(capture.Amount) >= Math.Round(nopOrder.OrderTotal, 2))
                            await _orderProcessingService.MarkOrderAsPaidAsync(nopOrder);

                        break;

                    case CaptureStatusType.DECLINED:
                    case CaptureStatusType.FAILED:
                        await _orderService.InsertOrderNoteAsync(new()
                        {
                            OrderId = nopOrder.Id,
                            Note = "The funds could not be captured",
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        break;

                    case CaptureStatusType.PARTIALLY_REFUNDED:
                    case CaptureStatusType.REFUNDED:

                        //processed by the refund object

                        break;
                }
            }

            //refund actions
            var refund = webhookResource as Refund;
            if (refund is not null && Enum.TryParse<RefundStatusType>(refund.Status, true, out var refundStatus))
            {
                switch (refundStatus)
                {
                    case RefundStatusType.CANCELLED:
                    case RefundStatusType.FAILED:
                        await _orderService.InsertOrderNoteAsync(new()
                        {
                            OrderId = nopOrder.Id,
                            Note = "The refund could not be processed or was cancelled",
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        break;

                    case RefundStatusType.COMPLETED:
                        var refundIds = await _genericAttributeService
                            .GetAttributeAsync<List<string>>(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName)
                            ?? new();
                        if (refundIds.Contains(refund.Id))
                            break;

                        if (!decimal.TryParse(refund.Amount?.Value, out var refundedAmount))
                            break;

                        if (!_orderProcessingService.CanPartiallyRefundOffline(nopOrder, refundedAmount))
                            break;

                        await _orderProcessingService.PartiallyRefundOfflineAsync(nopOrder, refundedAmount);

                        refundIds.Add(refund.Id);
                        await _genericAttributeService.SaveAttributeAsync(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName, refundIds);

                        break;

                    case RefundStatusType.PENDING:

                        //waiting the subsequent notification

                        break;
                }
            }

            //order actions
            var order = webhookResource as Order;
            if (order is not null && Enum.TryParse<OrderStatusType>(order.Status, true, out var orderStatus))
            {
                switch (orderStatus)
                {
                    case OrderStatusType.COMPLETED:
                        if (order.PurchaseUnits.FirstOrDefault().Payments?.Captures?.FirstOrDefault() is not Capture orderCapture)
                            break;

                        if (orderCapture.Status?.ToUpper() != CaptureStatusType.COMPLETED.ToString())
                            break;

                        if (!_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                            break;

                        nopOrder.CaptureTransactionId = orderCapture.Id;
                        nopOrder.CaptureTransactionResult = orderCapture.Status;

                        if (ConvertMoney(orderCapture.Amount) >= Math.Round(nopOrder.OrderTotal, 2))
                            await _orderProcessingService.MarkOrderAsPaidAsync(nopOrder);

                        break;
                }
            }

            await _orderService.UpdateOrderAsync(nopOrder);

            return true;
        });
    }

    #endregion

    #region Onboarding

    /// <summary>
    /// Prepare URL to sign up a merchant
    /// </summary>
    /// <param name="merchantGuid">Merchant internal id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the URL to sign up; error message if exists
    /// </returns>
    public async Task<((string SandboxUrl, string LiveUrl), string Error)> PrepareSignUpUrlAsync(string merchantGuid)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (string.IsNullOrEmpty(merchantGuid))
                throw new NopException("Merchant internal id is not set");

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var store = storeId > 0
                ? await _storeService.GetStoreByIdAsync(storeId)
                : await _storeContext.GetCurrentStoreAsync();
            var returnUrl = $"{store.Url.TrimEnd('/')}" +
                $"{_nopUrlHelper.RouteUrl(PayPalCommerceDefaults.Route.OnboardingCallback, new { storeId = storeId })}";

            //sandbox URL
            var onboarding = new Onboarding
            {
                Id = PayPalCommerceDefaults.Onboarding.Id.Sandbox,
                Product = PayPalProductType.PPCP.ToString().ToLower(),
                SecondaryProducts = string.Join(',',
                    PayPalProductType.PAYMENT_METHODS.ToString().ToLower(),
                    PayPalProductType.ADVANCED_VAULTING.ToString().ToLower()),
                Capabilities = string.Join(',',
                    ProductCapabilityType.APPLE_PAY.ToString().ToLower(),
                    ProductCapabilityType.GOOGLE_PAY.ToString().ToLower(),
                    ProductCapabilityType.PAYPAL_WALLET_VAULTING_ADVANCED.ToString().ToLower()),
                IntegrationType = IntegrationType.FO.ToString().ToUpper(),
                Features = string.Join(',',
                    FeatureType.PAYMENT.ToString().ToLower(),
                    FeatureType.REFUND.ToString().ToLower(),
                    FeatureType.ACCESS_MERCHANT_INFORMATION.ToString().ToLower(),
                    FeatureType.BILLING_AGREEMENT.ToString().ToLower(),
                    FeatureType.VAULT.ToString().ToLower()),
                ClientId = PayPalCommerceDefaults.Onboarding.ClientId.Sandbox,
                ReturnToUrl = returnUrl.ToLowerInvariant(),
                LogoUrl = PayPalCommerceDefaults.Onboarding.LogoUrl,
                SellerNonce = GetSha256Hash(merchantGuid),
                DisplayMode = "minibrowser"
            };
            var sandboxUrl = QueryHelpers.AddQueryString(PayPalCommerceDefaults.Onboarding.Url.Sandbox, ObjectToDictionary(onboarding));

            //live URL
            onboarding.Id = PayPalCommerceDefaults.Onboarding.Id.Live;
            onboarding.ClientId = PayPalCommerceDefaults.Onboarding.ClientId.Live;
            var liveUrl = QueryHelpers.AddQueryString(PayPalCommerceDefaults.Onboarding.Url.Live, ObjectToDictionary(onboarding));

            return (sandboxUrl, liveUrl);
        });
    }

    /// <summary>
    /// Sign up a merchant with the passed authentication parameters
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="authCode">Authentication parameters</param>
    /// <param name="sharedId">Authentication parameters</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the REST API application credentials; error message if exists
    /// </returns>
    public async Task<(Credentials Credentials, string Error)> SignUpAsync(PayPalCommerceSettings settings, string authCode, string sharedId)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (string.IsNullOrEmpty(settings.MerchantGuid))
                throw new NopException("Merchant internal id is not set");

            if (string.IsNullOrEmpty(sharedId) || string.IsNullOrEmpty(authCode))
                throw new NopException("Authentication parameters are empty");

            //try to get an access token
            var accessTokenRequest = new GetAccessTokenRequest
            {
                GrantType = "authorization_code",
                CodeVerifier = GetSha256Hash(settings.MerchantGuid.ToString()),
                Code = authCode,
                ClientId = sharedId,
                Secret = string.Empty
            };
            var accessToken = await _httpClient.RequestAsync<GetAccessTokenRequest, GetAccessTokenResponse>(accessTokenRequest, settings);

            //and change it to the credentials
            var credentialsRequest = new GetCredentialsRequest
            {
                Id = settings.UseSandbox ? PayPalCommerceDefaults.Onboarding.Id.Sandbox : PayPalCommerceDefaults.Onboarding.Id.Live,
                AccessToken = accessToken?.Token
            };
            var credentials = await _httpClient.RequestAsync<GetCredentialsRequest, GetCredentialsResponse>(credentialsRequest, settings);

            return credentials;
        });
    }

    /// <summary>
    /// Get the merchant details
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the merchant details; error message if exists
    /// </returns>
    public async Task<(Merchant Merchant, string Error)> GetMerchantAsync(PayPalCommerceSettings settings)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (string.IsNullOrEmpty(settings.MerchantGuid))
                throw new NopException("Merchant internal id is not set");

            //ensure that merchant id exists
            if (string.IsNullOrEmpty(settings.MerchantId))
                throw new NopException("Onboarding process failed, please try again");

            var request = new GetMerchantRequest
            {
                Id = settings.UseSandbox ? PayPalCommerceDefaults.Onboarding.Id.Sandbox : PayPalCommerceDefaults.Onboarding.Id.Live,
                MerchantId = settings.MerchantId
            };

            var merchant = await _httpClient.RequestAsync<GetMerchantRequest, GetMerchantResponse>(request, settings);

            //check capabilities statuses
            var ppcpStatus = merchant.Products
                ?.FirstOrDefault(product => product.Name?.ToUpper() == PayPalProductType.PPCP_CUSTOM.ToString())
                ?.VettingStatus?.ToUpper()
                ?? ProductStatusType.PENDING.ToString();
            var advancedProcessingEnabled = ppcpStatus == ProductStatusType.SUBSCRIBED.ToString();
            (bool Active, string Status) getCapability(ProductCapabilityType type)
            {
                var capabilityStatus = merchant.Capabilities
                    ?.FirstOrDefault(capability => capability.Name?.ToUpper() == type.ToString())
                    ?.Status?.ToUpper();
                var active = capabilityStatus == ProductCapabilityStatusType.ACTIVE.ToString();
                return (active && advancedProcessingEnabled, capabilityStatus ?? ProductCapabilityStatusType.PENDING.ToString());
            }

            merchant.AdvancedCards = getCapability(ProductCapabilityType.CUSTOM_CARD_PROCESSING);
            merchant.ApplePay = getCapability(ProductCapabilityType.APPLE_PAY);
            merchant.GooglePay = getCapability(ProductCapabilityType.GOOGLE_PAY);
            merchant.Vaulting = getCapability(ProductCapabilityType.PAYPAL_WALLET_VAULTING_ADVANCED);

            var review = ppcpStatus == ProductStatusType.IN_REVIEW.ToString();
            var needMoreData = ppcpStatus == ProductStatusType.NEED_MORE_DATA.ToString();
            var denied = ppcpStatus == ProductStatusType.DENIED.ToString();

            var cardsCapability = merchant.Capabilities
                ?.FirstOrDefault(capability => capability.Name?.ToUpper() == ProductCapabilityType.CUSTOM_CARD_PROCESSING.ToString());
            var withdrawCapability = merchant.Capabilities
                ?.FirstOrDefault(capability => capability.Name?.ToUpper() == ProductCapabilityType.WITHDRAW_MONEY.ToString());
            var sendMoneyCapability = merchant.Capabilities
                ?.FirstOrDefault(capability => capability.Name?.ToUpper() == ProductCapabilityType.SEND_MONEY.ToString());
            var inLimit = advancedProcessingEnabled &&
                merchant.AdvancedCards.Active &&
                cardsCapability?.Limits?.FirstOrDefault()?.Type?.ToUpper() == "GENERAL";
            var belowLimit = inLimit && withdrawCapability?.Limits is null && sendMoneyCapability?.Limits is null;
            var overLimit = inLimit && withdrawCapability?.Limits is not null && sendMoneyCapability?.Limits is not null;

            merchant.AdvancedCardsDetails = (review, needMoreData, belowLimit, overLimit, denied);

            merchant.ConfiguratorSupported = PayPalCommerceDefaults.PayLaterSupportedCountries.Contains(merchant.Country);

            return merchant;
        }, false);
    }

    #endregion

    #region Payment tokens

    /// <summary>
    /// Get customer's payment tokens
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="withDetails">Whether to load additional details of payment tokens</param>
    /// <param name="deleteTokenId">Identifier of the token to delete</param>
    /// <param name="defaultTokenId">Identifier of the token to mark as default</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of payment tokens; error message if exists
    /// </returns>
    public async Task<(List<PayPalToken> PaymentTokens, string Error)>
        GetPaymentTokensAsync(PayPalCommerceSettings settings, bool withDetails = false, int? deleteTokenId = null, int? defaultTokenId = null)
    {
        return await HandleFunctionAsync(async () =>
        {
            //only registered customers can save payment tokens
            var customer = await _workContext.GetCurrentCustomerAsync();
            if (await _customerService.IsGuestAsync(customer))
                return new List<PayPalToken>();

            //try to delete token
            if (deleteTokenId is not null)
            {
                var deleteToken = await _tokenService.GetByIdAsync(deleteTokenId.Value)
                    ?? throw new NopException("No payment token found with the specified id");

                if (deleteToken.CustomerId != customer.Id)
                    throw new NopException("You cannot delete this token");

                await _tokenService.DeleteAsync(deleteToken);
                await _paypalServerSdkClient.VaultController.DeletePaymentTokenAsync(deleteToken.VaultId);
            }

            //try to mark token as default
            if (defaultTokenId is not null)
            {
                var defaultToken = await _tokenService.GetByIdAsync(defaultTokenId.Value)
                    ?? throw new NopException("No payment token found with the specified id");

                if (defaultToken.CustomerId != customer.Id)
                    throw new NopException("You cannot edit this token");

                defaultToken.IsPrimaryMethod = true;
                await _tokenService.UpdateAsync(defaultToken);

                var tokensToUpdate = (await _tokenService.GetAllTokensAsync(settings.ClientId, customer.Id))
                    .Where(token => token.Id != defaultToken.Id && token.IsPrimaryMethod);
                foreach (var token in tokensToUpdate)
                {
                    token.IsPrimaryMethod = false;
                    await _tokenService.UpdateAsync(token);
                }
            }

            var tokens = await _tokenService.GetAllTokensAsync(settings.ClientId, customer.Id);
            if (!tokens.Any())
                return new List<PayPalToken>();

            if (!withDetails)
                return tokens.ToList();

            //load additional details
            return await PreparePaymentTokensAsync(settings, tokens);
        });
    }

    /// <summary>
    /// Delete all customer's payment tokens
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="customerId">Customer id</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the delete result; error message if exists
    /// </returns>
    public async Task<(bool Result, string Error)> DeletePaymentTokensAsync(PayPalCommerceSettings settings, int customerId)
    {
        return await HandleFunctionAsync(async () =>
        {
            var tokens = await _tokenService.GetAllTokensAsync(settings.ClientId, customerId);
            await _tokenService.DeleteAsync(tokens);
            foreach (var token in tokens)
            {
                try
                {
                    await _paypalServerSdkClient.VaultController.DeletePaymentTokenAsync(token.VaultId);
                }
                catch { }
            }

            return true;
        });
    }

    /// <summary>
    /// Get previously saved cards (payment tokens)
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <param name="placement">Button placement</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of payment tokens; error message if exists
    /// </returns>
    public async Task<(List<PayPalToken> PaymentTokens, string Error)> GetSavedCardsAsync(PayPalCommerceSettings settings, ButtonPlacement placement)
    {
        return await HandleFunctionAsync(async () =>
        {
            if (placement != ButtonPlacement.PaymentMethod || !settings.UseCardFields || !settings.UseVault)
                return null;

            var customer = await _workContext.GetCurrentCustomerAsync();
            if (await _customerService.IsGuestAsync(customer))
                return null;

            //get cards only
            var tokens = await _tokenService.GetAllTokensAsync(settings.ClientId, customer.Id, type: nameof(PaymentSource.Card));

            return await PreparePaymentTokensAsync(settings, tokens);
        });
    }

    #endregion

    #endregion

    #region Nested classes

    /// <summary>
    /// Represents the shopping cart details
    /// </summary>
    private class CartDetails
    {
        #region Properties

        /// <summary>
        /// Gets or sets the button placement
        /// </summary>
        public ButtonPlacement Placement { get; set; }

        /// <summary>
        /// Gets or sets the current customer
        /// </summary>
        public Customer Customer { get; set; }

        /// <summary>
        /// Gets or sets the current store
        /// </summary>
        public Store Store { get; set; }

        /// <summary>
        /// Gets or sets the customer's shopping cart
        /// </summary>
        public List<ShoppingCartItem> Cart { get; set; } = new();

        /// <summary>
        /// Gets or sets the primary store currency code
        /// </summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Gets or sets the customer's billing address
        /// </summary>
        public NopAddress BillingAddress { get; set; }

        /// <summary>
        /// Gets or sets the customer's shipping address
        /// </summary>
        public NopAddress ShippingAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the shipping is required for this cart
        /// </summary>
        public bool ShippingIsRequired { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the pick up in store option is selected
        /// </summary>
        public bool IsPickup { get; set; }

        /// <summary>
        /// Gets or sets the selected shipping option
        /// </summary>
        public NopShippingOption ShippingOption { get; set; }

        /// <summary>
        /// Gets or sets the selected pickup point
        /// </summary>
        public PickupPoint PickupPoint { get; set; }

        #endregion
    }

    #endregion
}