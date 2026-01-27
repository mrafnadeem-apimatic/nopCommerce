using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.PayPal.Services;

namespace Nop.Plugin.Payments.PayPal.Infrastructure;

/// <summary>
/// Represents object for configuring services on application startup
/// </summary>
public class NopStartup : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <summary>
    /// Registers services required by the PayPal plugin.
    /// </summary>
    /// <remarks>
    /// Adds PayPalHttpClient to the dependency injection container with a scoped lifetime.
    /// </remarks>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        //register PayPal HTTP client helper
        services.AddScoped<PayPalHttpClient>();
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <summary>
    /// Provides a hook to configure the application's HTTP request pipeline for the PayPal plugin.
    /// </summary>
    /// <param name="application">The application builder used to register middleware components.</param>
    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 2;
}

