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
