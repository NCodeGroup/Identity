// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="ICookieManager"/> of the interactive session cookie. It scopes every cookie it writes or deletes to
/// the path of the request's tenant, so tenants that share an origin under the path strategy keep separate sessions:
/// the browser returns a tenant's session cookie only to that tenant's endpoints.
/// </summary>
internal sealed class InteractiveSessionCookieManager(InteractiveSurfacePaths surfacePaths)
    : ICookieManager
{
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;
    private ChunkingCookieManager Inner { get; } = new();

    /// <inheritdoc />
    public string? GetRequestCookie(HttpContext context, string key) =>
        Inner.GetRequestCookie(context, key);

    /// <inheritdoc />
    public void AppendResponseCookie(
        HttpContext context,
        string key,
        string? value,
        CookieOptions options
    ) => Inner.AppendResponseCookie(context, key, value, ScopeToTenant(context, options));

    /// <inheritdoc />
    public void DeleteCookie(HttpContext context, string key, CookieOptions options) =>
        Inner.DeleteCookie(context, key, ScopeToTenant(context, options));

    private CookieOptions ScopeToTenant(HttpContext context, CookieOptions options)
    {
        var tenantPath = SurfacePaths.GetTenantPath(context);
        return new CookieOptions(options) { Path = tenantPath.HasValue ? tenantPath.Value : "/" };
    }
}
