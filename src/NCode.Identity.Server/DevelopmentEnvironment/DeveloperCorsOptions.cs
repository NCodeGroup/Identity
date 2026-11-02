// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Configures the <strong>development-only</strong> cross-origin (CORS) policy that lets a local single-page
/// application dev server call the OpenID server. Bound from the <c>Cors</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class DeveloperCorsOptions
{
    /// <summary>
    /// The configuration section (<c>Cors</c>) bound to <see cref="DeveloperCorsOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "Cors";

    /// <summary>
    /// Gets or sets the browser origins allowed to call the server cross-origin, each written exactly as a browser
    /// sends it in the <c>Origin</c> header: an <c>http</c> or <c>https</c> scheme and a host, with an optional port
    /// and no path, query, or trailing slash (for example <c>https://app.example.com</c>). An entry in any other form
    /// can never match a request, so it stops the host when it starts. The section is the opt-in: without it no policy
    /// is registered.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
