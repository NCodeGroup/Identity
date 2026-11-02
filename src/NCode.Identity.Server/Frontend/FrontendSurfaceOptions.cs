// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.Frontend;

/// <summary>
/// Configures the per-surface overrides a single bootstrap endpoint serves to each frontend surface. Only
/// the coordinates that differ from the host default need to be set; an unset member falls back to the default, except
/// <see cref="ClientId"/> and <see cref="Scopes"/>, which replace the default so a read-only surface can publish no
/// client at all.
/// </summary>
// See ADR-1064.
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
