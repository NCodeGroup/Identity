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

namespace NCode.Identity.Server;

/// <summary>
/// Configures the runtime bootstrap document the OpenID Provider host publishes for the tier-agnostic frontend
/// bundles (ADR-0064). A single bundle is built once and discovers its deployment coordinates at boot by fetching
/// this document from the serving host, so every value here is per-deployment and none is baked at build time.
/// When <see cref="ApiBaseUrl"/> or <see cref="Issuer"/> is left unset, the serving request's own origin
/// (<c>scheme://host</c>) is used, so a single-origin host needs no configuration at all.
/// </summary>
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
    /// It must byte-match the discovery <c>issuer</c> the bundle reads. When unset, the serving request's own origin
    /// is used.
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

/// <summary>
/// Configures the per-surface overrides a single bootstrap endpoint serves to each frontend surface (ADR-0064). Only
/// the coordinates that differ from the host default need to be set; an unset member falls back to the default, except
/// <see cref="ClientId"/> and <see cref="Scopes"/>, which replace the default so a read-only surface can publish no
/// client at all.
/// </summary>
[PublicAPI]
public sealed class FrontendSurfaceOptions
{
    /// <summary>
    /// Gets or sets the public OAuth client identifier this surface authenticates as (authorization-code + PKCE).
    /// When unset, this surface publishes no client id (a read-only surface that performs no interactive login).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the scopes this surface requests at login. When unset, the surface applies its own defaults.
    /// </summary>
    public string[]? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the feature flags for this surface. Each entry overrides the default for that key; keys absent
    /// here inherit the default.
    /// </summary>
    public IDictionary<string, bool>? Features { get; set; }

    /// <summary>
    /// Gets or sets the branding overrides for this surface. When unset or empty, the default branding is used.
    /// </summary>
    public FrontendBrandingOptions? Branding { get; set; }
}

/// <summary>
/// Configures the brandable theme tokens injected into the frontend bundles at runtime (ADR-0064 branding seam).
/// </summary>
[PublicAPI]
public sealed class FrontendBrandingOptions
{
    /// <summary>
    /// Gets or sets the product/display name shown in the bundle's chrome.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the logo to render.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the primary theme color.
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// Gets or sets the secondary theme color.
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Gets or sets the default color mode (<c>light</c> or <c>dark</c>).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Gets a value indicating whether any branding token has been set, so an all-empty section is omitted
    /// from the published document.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(DisplayName)
        && string.IsNullOrWhiteSpace(LogoUrl)
        && string.IsNullOrWhiteSpace(PrimaryColor)
        && string.IsNullOrWhiteSpace(SecondaryColor)
        && string.IsNullOrWhiteSpace(Mode);
}
