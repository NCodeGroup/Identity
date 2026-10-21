#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Locates the interactive surface for a request. The surface is served under the tenant route, like the protocol
/// endpoints (ADR-0138), so every redirect it issues and the session cookie it scopes start from the path the request's
/// tenant is served under.
/// </summary>
internal sealed class InteractiveSurfacePaths(
    ITenantResolver tenantResolver,
    TemplateBinderFactory templateBinderFactory
)
{
    private ITenantResolver TenantResolver { get; } = tenantResolver;
    private TemplateBinderFactory TemplateBinderFactory { get; } = templateBinderFactory;
    private TemplateBinder? TenantRouteBinderOrNull { get; set; }

    /// <summary>
    /// Gets the path the request's tenant is served under: the request's path base followed by the tenant route,
    /// bound from a copy of the request's values for that route's own parameters (for example <c>/acme</c>). A request
    /// that does not carry the tenant route's values yields the path base alone.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <returns>The tenant's path, which is empty for a tenant served at the root of a host with no path base.</returns>
    public PathString GetTenantPath(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var tenantRoute = TenantResolver.GetTenantRoute();
        if (tenantRoute.PathSegments.Count == 0)
        {
            return request.PathBase;
        }

        var values = new RouteValueDictionary();
        foreach (var parameter in tenantRoute.Parameters)
        {
            if (request.RouteValues.TryGetValue(parameter.Name, out var value))
            {
                values[parameter.Name] = value;
            }
        }

        var binder = TenantRouteBinderOrNull ??= TemplateBinderFactory.Create(tenantRoute);
        var tenantPath = binder.BindValues(values)?.TrimEnd('/');
        return string.IsNullOrEmpty(tenantPath)
            ? request.PathBase
            : request.PathBase.Add(new PathString(tenantPath));
    }

    /// <summary>
    /// Gets a path of the interactive surface, such as <see cref="InteractiveAuthenticationDefaults.LoginPath"/>, as
    /// the request's tenant serves it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="surfacePath">The surface path, relative to the tenant.</param>
    /// <returns>The surface path beneath the request's tenant path.</returns>
    public string GetSurfacePath(HttpContext httpContext, string surfacePath) =>
        GetTenantPath(httpContext).Add(new PathString(surfacePath)).ToString();

    /// <summary>
    /// Gets the base of the interactive surface as the request's tenant serves it (for example <c>/acme/account/</c>):
    /// the <c>&lt;base href&gt;</c> of every page the surface serves, against which the bundle resolves its assets,
    /// API calls, and page links.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <returns>The surface's base path, ending with <c>/</c>.</returns>
    public string GetSurfaceBase(HttpContext httpContext) =>
        GetSurfacePath(httpContext, InteractiveAuthenticationDefaults.BasePath) + "/";
}
