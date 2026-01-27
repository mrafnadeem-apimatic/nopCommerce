using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Authentication;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Onboarding;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Orders;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Payments;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.PaymentTokens;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Controllers;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using Environment = System.Environment;
using PaypalModels = PaypalServerSdk.Standard.Models;

namespace Nop.Plugin.Payments.PayPalCommerce.Services;

/// <summary>
/// Represents the HTTP client to request PayPal API
/// </summary>
public class PayPalCommerceHttpClient
{
    #region Fields

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerSettings _serializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static Dictionary<string, AccessToken> _accessTokens = new();

    #endregion

    #region Ctor

    public PayPalCommerceHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Get access token
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the access token
    /// </returns>
    private async Task<string> GetAccessTokenAsync(PayPalCommerceSettings settings)
    {
        if (!PayPalCommerceServiceManager.IsConfigured(settings))
            throw new NopException("Plugin is not configured");

        //no need to request a token if there is already a cached one and it has not expired (lifetime is about 9 hours)
        if (!_accessTokens.TryGetValue(settings.ClientId, out var accessToken) ||
            string.IsNullOrEmpty(accessToken?.Token) ||
            accessToken.IsExpired)
        {
            //get new access token
            accessToken = await RequestAsync<GetAccessTokenRequest, GetAccessTokenResponse>(new()
            {
                ClientId = settings.ClientId,
                Secret = settings.SecretKey,
                GrantType = "client_credentials"
            }, settings);
            _accessTokens[settings.ClientId] = accessToken;
        }

        return accessToken.Token;
    }

    /// <summary>
    /// Create configured PayPal Server SDK client
    /// </summary>
    /// <param name="settings">Plugin settings</param>
    /// <returns>SDK client instance</returns>
    private static PaypalServerSdkClient CreateSdkClient(PayPalCommerceSettings settings)
    {
        var environment = settings.UseSandbox
            ? PaypalServerSdk.Standard.Environment.Sandbox
            : PaypalServerSdk.Standard.Environment.Production;

        return new PaypalServerSdkClient.Builder()
            .ClientCredentialsAuth(
                new ClientCredentialsAuthModel.Builder(
                    settings.ClientId,
                    settings.SecretKey
                ).Build())
            .Environment(environment)
            .LoggingConfig(config => config
                .LogLevel(LogLevel.Information)
                .RequestConfig(reqConfig => reqConfig.Body(true))
                .ResponseConfig(respConfig => respConfig.Headers(true)))
            .Build();
    }

    private static async Task<TPluginResponse> ExecuteSdkCallAsync<TPluginResponse, TSdkResponse>(
        PayPalCommerceSettings settings,
        Func<PaypalServerSdkClient, Task<ApiResponse<TSdkResponse>>> call)
        where TPluginResponse : class, IApiResponse
    {
        var client = CreateSdkClient(settings);

        try
        {
            var apiResponse = await call(client);

            //some endpoints don't return a body from the original implementation
            if (typeof(TPluginResponse) == typeof(EmptyResponse))
                return default;

            if (apiResponse is null)
                throw new NopException("Failed request", new NopException("Empty response from PayPal API"));

            if (apiResponse.Data is null)
            {
                var message = $"Empty response body from PayPal API for '{typeof(TPluginResponse).Name}'.";
                throw new NopException("Failed request", new NopException(message));
            }

            var responseJson = JsonConvert.SerializeObject(apiResponse.Data, _serializerSettings);
            return JsonConvert.DeserializeObject<TPluginResponse>(responseJson);
        }
        catch (ApiException ex)
        {
            var message = BuildApiExceptionMessage(ex);
            throw new NopException("Failed request", new NopException(message));
        }
    }

    private static async Task ExecuteSdkCallAsync(
        PayPalCommerceSettings settings,
        Func<PaypalServerSdkClient, Task> call)
    {
        var client = CreateSdkClient(settings);

        try
        {
            await call(client);
        }
        catch (ApiException ex)
        {
            var message = BuildApiExceptionMessage(ex);
            throw new NopException("Failed request", new NopException(message));
        }
    }

    private static string BuildApiExceptionMessage(ApiException ex)
    {
        var message = ex.Message;

        if (ex is ErrorException errorException)
        {
            var errorDetails = new
            {
                errorException.Name,
                errorException.Message,
                errorException.DebugId,
                errorException.Details,
                errorException.Links
            };

            message += $"{Environment.NewLine}{JsonConvert.SerializeObject(errorDetails, Formatting.Indented)}";
        }

        return message;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Request remote service
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="request">Request</param>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the response details
    /// </returns>
    public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, PayPalCommerceSettings settings)
        where TRequest : IApiRequest where TResponse : IApiResponse
    {
        //route selected operations through PayPal Server SDK
        switch (request)
        {
            case CreateOrderRequest createOrderRequest when typeof(TResponse) == typeof(CreateOrderResponse):
                {
                    var result = await ExecuteSdkCallAsync<CreateOrderResponse, PaypalModels.Order>(
                        settings,
                        async client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(createOrderRequest, _serializerSettings);
                            var sdkBody = JsonConvert.DeserializeObject<PaypalModels.OrderRequest>(bodyJson);

                            var input = new PaypalModels.CreateOrderInput
                            {
                                Body = sdkBody,
                                Prefer = "return=representation"
                            };

                            return await client.OrdersController.CreateOrderAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case GetOrderRequest getOrderRequest when typeof(TResponse) == typeof(GetOrderResponse):
                {
                    var result = await ExecuteSdkCallAsync<GetOrderResponse, PaypalModels.Order>(
                        settings,
                        client =>
                        {
                            var input = new PaypalModels.GetOrderInput
                            {
                                Id = getOrderRequest.OrderId,
                                Fields = getOrderRequest.Fields
                            };

                            return client.OrdersController.GetOrderAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case UpdateOrderRequest<object> updateOrderRequest when typeof(TResponse) == typeof(EmptyResponse):
                {
                    await ExecuteSdkCallAsync(
                        settings,
                        client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(updateOrderRequest, _serializerSettings);
                            var sdkPatches = JsonConvert.DeserializeObject<List<PaypalModels.Patch>>(bodyJson) ?? new();

                            var input = new PaypalModels.PatchOrderInput
                            {
                                Id = updateOrderRequest.OrderId,
                                Body = sdkPatches
                            };

                            return client.OrdersController.PatchOrderAsync(input);
                        });

                    return default;
                }

            case CreateAuthorizationRequest createAuthorizationRequest when typeof(TResponse) == typeof(CreateAuthorizationResponse):
                {
                    var result = await ExecuteSdkCallAsync<CreateAuthorizationResponse, PaypalModels.OrderAuthorizeResponse>(
                        settings,
                        client =>
                        {
                            var input = new PaypalModels.AuthorizeOrderInput
                            {
                                Id = createAuthorizationRequest.OrderId,
                                Prefer = "return=representation"
                            };

                            return client.OrdersController.AuthorizeOrderAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case Services.Api.Orders.CreateCaptureRequest createOrderCaptureRequest when typeof(TResponse) == typeof(Services.Api.Orders.CreateCaptureResponse):
                {
                    var result = await ExecuteSdkCallAsync<Services.Api.Orders.CreateCaptureResponse, PaypalModels.Order>(
                        settings,
                        client =>
                        {
                            var input = new PaypalModels.CaptureOrderInput
                            {
                                Id = createOrderCaptureRequest.OrderId,
                                Prefer = "return=representation"
                            };

                            return client.OrdersController.CaptureOrderAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case CreateTrackingRequest createTrackingRequest when typeof(TResponse) == typeof(CreateTrackingResponse):
                {
                    var result = await ExecuteSdkCallAsync<CreateTrackingResponse, PaypalModels.Order>(
                        settings,
                        client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(createTrackingRequest, _serializerSettings);
                            var sdkBody = JsonConvert.DeserializeObject<PaypalModels.OrderTrackerRequest>(bodyJson);

                            var input = new PaypalModels.CreateOrderTrackingInput
                            {
                                Id = createTrackingRequest.OrderId,
                                Body = sdkBody
                            };

                            return client.OrdersController.CreateOrderTrackingAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case Services.Api.Payments.CreateCaptureRequest createPaymentCaptureRequest when typeof(TResponse) == typeof(Services.Api.Payments.CreateCaptureResponse):
                {
                    var result = await ExecuteSdkCallAsync<Services.Api.Payments.CreateCaptureResponse, PaypalModels.CapturedPayment>(
                        settings,
                        client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(createPaymentCaptureRequest, _serializerSettings);
                            var sdkBody = JsonConvert.DeserializeObject<PaypalModels.CaptureRequest>(bodyJson);

                            var input = new PaypalModels.CaptureAuthorizedPaymentInput
                            {
                                AuthorizationId = createPaymentCaptureRequest.AuthorizationId,
                                Prefer = "return=representation",
                                Body = sdkBody
                            };

                            return client.PaymentsController.CaptureAuthorizedPaymentAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case CreateVoidRequest createVoidRequest when typeof(TResponse) == typeof(EmptyResponse):
                {
                    await ExecuteSdkCallAsync(
                        settings,
                        client =>
                        {
                            var input = new PaypalModels.VoidPaymentInput
                            {
                                AuthorizationId = createVoidRequest.AuthorizationId,
                                Prefer = "return=representation"
                            };

                            return client.PaymentsController.VoidPaymentAsync(input);
                        });

                    return default;
                }

            case CreateRefundRequest createRefundRequest when typeof(TResponse) == typeof(CreateRefundResponse):
                {
                    var result = await ExecuteSdkCallAsync<CreateRefundResponse, PaypalModels.Refund>(
                        settings,
                        client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(createRefundRequest, _serializerSettings);
                            var sdkBody = JsonConvert.DeserializeObject<PaypalModels.RefundRequest>(bodyJson);

                            var input = new PaypalModels.RefundCapturedPaymentInput
                            {
                                CaptureId = createRefundRequest.CaptureId,
                                Prefer = "return=representation",
                                Body = sdkBody
                            };

                            return client.PaymentsController.RefundCapturedPaymentAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case CreateSetupTokenRequest createSetupTokenRequest when typeof(TResponse) == typeof(CreateSetupTokenResponse):
                {
                    var result = await ExecuteSdkCallAsync<CreateSetupTokenResponse, PaypalModels.SetupTokenResponse>(
                        settings,
                        client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(createSetupTokenRequest, _serializerSettings);
                            var sdkBody = JsonConvert.DeserializeObject<PaypalModels.SetupTokenRequest>(bodyJson);

                            var input = new PaypalModels.CreateSetupTokenInput
                            {
                                Body = sdkBody
                            };

                            return client.VaultController.CreateSetupTokenAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case CreatePaymentTokenRequest createPaymentTokenRequest when typeof(TResponse) == typeof(CreatePaymentTokenResponse):
                {
                    var result = await ExecuteSdkCallAsync<CreatePaymentTokenResponse, PaypalModels.PaymentTokenResponse>(
                        settings,
                        client =>
                        {
                            var bodyJson = JsonConvert.SerializeObject(createPaymentTokenRequest, _serializerSettings);
                            var sdkBody = JsonConvert.DeserializeObject<PaypalModels.PaymentTokenRequest>(bodyJson);

                            var input = new PaypalModels.CreatePaymentTokenInput
                            {
                                Body = sdkBody
                            };

                            return client.VaultController.CreatePaymentTokenAsync(input);
                        });

                    return (TResponse)(object)result;
                }

            case DeletePaymentTokenRequest deletePaymentTokenRequest when typeof(TResponse) == typeof(EmptyResponse):
                {
                    await ExecuteSdkCallAsync(
                        settings,
                        client => client.VaultController.DeletePaymentTokenAsync(deletePaymentTokenRequest.Id));

                    return default;
                }
        }

        //fallback to legacy HttpClient implementation for unsupported operations

        //prepare request body, content is always JSON except for access token requests
        var requestString = JsonConvert.SerializeObject(request, _serializerSettings);
        var requestContent = request is GetAccessTokenRequest accessTokenRequest
            ? new FormUrlEncodedContent(PayPalCommerceServiceManager.ObjectToDictionary(accessTokenRequest))
            : (ByteArrayContent)new StringContent(requestString, Encoding.Default, MimeTypes.ApplicationJson);

        //URL depends on environment
        var baseUrl = settings.UseSandbox
            ? PayPalCommerceDefaults.ServiceUrl.Sandbox
            : PayPalCommerceDefaults.ServiceUrl.Live;

        var requestMessage = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(new Uri(baseUrl), request.Path))
        {
            Content = requestContent
        };

        //set timeout
        try
        {
            var timeout = TimeSpan.FromSeconds(settings.RequestTimeout ?? PayPalCommerceDefaults.RequestTimeout);
            if (_httpClient.Timeout != timeout)
                _httpClient.Timeout = timeout;
        }
        catch { }

        //add authorization and some custom headers
        var authorization = request switch
        {
            IAuthorizedRequest => $"Bearer {await GetAccessTokenAsync(settings)}",
            GetCredentialsRequest credentialsRequest => $"Bearer {credentialsRequest.AccessToken}",
            GetAccessTokenRequest tokenRequest =>
                $"Basic {Convert.ToBase64String(Encoding.Default.GetBytes($"{tokenRequest.ClientId}:{tokenRequest.Secret}"))}",
            _ => null
        };
        if (!string.IsNullOrEmpty(authorization))
            requestMessage.Headers.Add(HeaderNames.Authorization, authorization);
        requestMessage.Headers.Add(HeaderNames.UserAgent, PayPalCommerceDefaults.UserAgent);
        requestMessage.Headers.Add(HeaderNames.Accept, MimeTypes.ApplicationJson);
        requestMessage.Headers.Add(PayPalCommerceDefaults.PartnerHeader.Name, PayPalCommerceDefaults.PartnerHeader.Value);
        requestMessage.Headers.Add("PayPal-Request-Id", Guid.NewGuid().ToString());
        requestMessage.Headers.Add("Prefer", "return=representation");

        //execute the request and get a result
        var httpResponse = await _httpClient.SendAsync(requestMessage);
        var responseString = await httpResponse.Content.ReadAsStringAsync();

        //successful request processing
        if (httpResponse.IsSuccessStatusCode)
        {
            if (typeof(TResponse) == typeof(EmptyResponse))
                return default;

            return JsonConvert.DeserializeObject<TResponse>(responseString ?? string.Empty) ?? default;
        }

        //failed request processing
        var error = $"Failed request ({httpResponse.StatusCode})";
        var identityErrorResponse = JsonConvert.DeserializeObject<IdentityErrorResponse>(responseString ?? string.Empty);
        if (!string.IsNullOrEmpty(identityErrorResponse?.Error))
        {
            var description = !string.IsNullOrEmpty(identityErrorResponse.ErrorDescription)
                ? identityErrorResponse.ErrorDescription
                : identityErrorResponse.Error;
            error += $": {description}";
        }

        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseString ?? string.Empty);
        if (!string.IsNullOrEmpty(errorResponse?.Name))
        {
            error += $": {(!string.IsNullOrEmpty(errorResponse.Message) ? errorResponse.Message : errorResponse.Name)}";
            error += $"{Environment.NewLine}{JsonConvert.SerializeObject(errorResponse, Formatting.Indented)}";
        }

        throw new NopException("Failed request", new NopException(error));
    }

    #endregion
}