using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.PayPalCommerce.Factories;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Web.Framework.Infrastructure.Extensions;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;

namespace Nop.Plugin.Payments.PayPalCommerce.Infrastructure;

/// <summary>
/// Represents the object for the configuring services on application startup
/// </summary>
public class NopStartup : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="configuration">Configuration of the application</param>
    public void ConfigureServices(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.AddHttpClient<OnboardingHttpClient>().WithProxy();
        services.AddHttpClient<PayPalCommerceHttpClient>().WithProxy();
        
        services.AddScoped<PaypalServerSdkClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<PayPalCommerceSettings>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            var environment = settings.UseSandbox
                ? PaypalServerSdk.Standard.Environment.Sandbox
                : PaypalServerSdk.Standard.Environment.Production;

            var credentials = new ClientCredentialsAuthModel.Builder(
                settings.ClientId,
                settings.SecretKey
            ).Build();

            var builder = new PaypalServerSdkClient.Builder()
                .ClientCredentialsAuth(credentials)
                .Environment(environment)
                .LoggingConfig(config => config
                    .LogLevel(LogLevel.Information));

            builder.HttpClientConfig(config =>
            {
                // Use IHttpClientFactory to provide the underlying HttpClient
                var httpClient = httpClientFactory.CreateClient();
                config.HttpClientInstance(httpClient);

                if (settings.RequestTimeout.HasValue && settings.RequestTimeout.Value > 0)
                    config.Timeout(TimeSpan.FromSeconds(settings.RequestTimeout.Value));
            });

            return builder.Build();
        });

        services.AddScoped<PayPalCommerceModelFactory>();
        services.AddScoped<PayPalCommerceServiceManager>();
        services.AddScoped<PayPalTokenService>();
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 1;
}