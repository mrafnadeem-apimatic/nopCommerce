using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// Custom controller feature provider that limits discovered controllers
/// to nopCommerce assemblies, so external SDK "Controller" types
/// (like PaypalServerSdk) are not treated as MVC controllers.
/// </summary>
public class NopControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        // First apply the default MVC rules
        if (!base.IsController(typeInfo))
            return false;

        var assemblyName = typeInfo.Assembly.GetName().Name;

        // Only treat types from nopCommerce assemblies as MVC controllers
        // This avoids picking up external SDK client classes such as
        // PaypalServerSdk.Standard.Controllers.* which are not real MVC controllers.
        if (string.IsNullOrEmpty(assemblyName) ||
            !assemblyName.StartsWith("Nop", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }
}