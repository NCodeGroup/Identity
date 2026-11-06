// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the security headers that <c>UseBffSecurityHeaders()</c> writes on every response that does not set
/// them itself. The defaults are a strict starting point; the Content-Security-Policy depends on the application.
/// </summary>
[PublicAPI]
public sealed class BffSecurityHeadersOptions
{
    /// <summary>
    /// Gets or sets the <c>Content-Security-Policy</c> header. The default is
    /// <c>default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'</c>. It lists no
    /// <c>form-action</c>, because browsers check that directive against the redirect that follows the logout form,
    /// which leaves the origin for the OpenID Provider. The configuration key is
    /// <c>Bff:Frontends:{name}:SecurityHeaders:ContentSecurityPolicy</c>.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    /// <summary>
    /// Gets or sets the <c>Referrer-Policy</c> header. The default is <c>same-origin</c>, so no other origin learns
    /// the frontend's URLs. The configuration key is <c>Bff:Frontends:{name}:SecurityHeaders:ReferrerPolicy</c>.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "same-origin";

    /// <summary>
    /// Gets or sets the <c>Cross-Origin-Opener-Policy</c> header. The default is <c>same-origin</c>, so a window the
    /// frontend opens on another origin, or that opened it, holds no reference to it. The configuration key is
    /// <c>Bff:Frontends:{name}:SecurityHeaders:CrossOriginOpenerPolicy</c>.
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";
}
