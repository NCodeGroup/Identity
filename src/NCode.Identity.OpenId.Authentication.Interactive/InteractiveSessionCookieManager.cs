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

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="ICookieManager"/> of the interactive session cookie. The cookie is host-only (<c>Path=/</c>, no
/// <c>Domain</c>, <c>Secure</c>), and tenants that share an origin under the path strategy are separated by name: a
/// tenant served under a path gets a cookie name qualified by a hash of that path (ADR-1310).
/// </summary>
internal sealed class InteractiveSessionCookieManager(InteractiveSurfacePaths surfacePaths)
    : ICookieManager
{
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;
    private ChunkingCookieManager Inner { get; } = new();

    /// <inheritdoc />
    public string? GetRequestCookie(HttpContext context, string key) =>
        Inner.GetRequestCookie(context, GetTenantCookieName(context, key));

    /// <inheritdoc />
    public void AppendResponseCookie(
        HttpContext context,
        string key,
        string? value,
        CookieOptions options
    ) =>
        Inner.AppendResponseCookie(
            context,
            GetTenantCookieName(context, key),
            value,
            ToHostOnly(options)
        );

    /// <inheritdoc />
    public void DeleteCookie(HttpContext context, string key, CookieOptions options) =>
        Inner.DeleteCookie(context, GetTenantCookieName(context, key), ToHostOnly(options));

    private string GetTenantCookieName(HttpContext context, string key)
    {
        var tenantPath = SurfacePaths.GetTenantPath(context);
        if (!tenantPath.HasValue)
        {
            return key;
        }

        var hash = SHA256.HashData(SecureEncoding.UTF8.GetBytes(tenantPath.Value));
        return $"{key}.{Base64Url.EncodeToString(hash)}";
    }

    private static CookieOptions ToHostOnly(CookieOptions options) =>
        new(options)
        {
            Path = "/",
            Domain = null,
            Secure = true,
        };
}
