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

using JetBrains.Annotations;

namespace NCode.Identity.Server.Frontend;

/// <summary>
/// Configures the runtime bootstrap document the OpenID Provider host publishes for the tier-agnostic frontend
/// bundles. A single bundle is built once and discovers its deployment coordinates at boot by fetching
/// this document from the serving host, so every value here is per-deployment and none is baked at build time.
/// When <see cref="Issuer"/> is left unset, the issuer of the tenant that serves the surface is used; when
/// <see cref="ApiBaseUrl"/> is left unset, the serving request's own origin (<c>scheme://host</c>) is used. A host
/// therefore needs no configuration at all.
/// </summary>
// See ADR-0064.
[PublicAPI]
public sealed class FrontendBootstrapOptions
{
    /// <summary>
    /// Gets the default configuration section name that holds the frontend bootstrap options.
    /// </summary>
    public const string DefaultSectionName = "Frontend";

    /// <summary>
    /// Gets or sets the base URL of the Management/OpenID REST API the bundle calls. When unset, the serving
    /// request's own origin is used.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the OpenID issuer whose <c>/.well-known/openid-configuration</c> describes the auth endpoints.
    /// It must byte-match the discovery <c>issuer</c> the bundle reads. When unset, the issuer of the tenant that serves
    /// the surface is used: the tenant the request identifies (the only tenant under the static single-tenant strategy),
    /// and otherwise the root tenant, which serves the Management console. It is never derived from the request.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the public OAuth client identifier the bundle authenticates as (authorization-code + PKCE).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the scopes the bundle requests at login. When unset or empty, the bundle applies its own
    /// defaults.
    /// </summary>
    public string[]? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the feature flags toggled per deployment (for example, dynamic client registration).
    /// </summary>
    public IDictionary<string, bool> Features { get; set; } =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the optional branding overrides resolved per deployment.
    /// </summary>
    public FrontendBrandingOptions? Branding { get; set; }

    /// <summary>
    /// Gets or sets the per-surface coordinate overrides, keyed by surface name (for example, <c>playground</c> or
    /// <c>conformance</c>). Every surface shares one bootstrap endpoint; when a bundle requests its coordinates it
    /// passes its surface name, and the matching override here supplies that surface's own public client id, scopes,
    /// feature flags, and branding. The top-level members above are the default (served to the Management console and
    /// to any request that names no surface); each override replaces — it does not merge into — the default client id
    /// and scopes, so a read-only surface can publish <em>no</em> client by omitting one. A requested surface that has
    /// no entry here falls back to the default.
    /// </summary>
    public IDictionary<string, FrontendSurfaceOptions> Surfaces { get; set; } =
        new Dictionary<string, FrontendSurfaceOptions>(StringComparer.Ordinal);
}
