using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.PayPal.Services;

namespace Nop.Plugin.Payments.PayPal.Infrastructure;

/// <summary>
/// Represents object for configuring PayPal payment plugin services on application startup.
/// </summary>
public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PayPalOrdersService>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 1;
}

