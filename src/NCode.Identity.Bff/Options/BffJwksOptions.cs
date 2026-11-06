// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the optional endpoint that publishes the BFF's public client-authentication keys.
/// </summary>
[PublicAPI]
public sealed class BffJwksOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether <c>MapBffEndpoints()</c> maps <c>GET {PathPrefix}/jwks</c>, which
    /// publishes the public keys of <c>Bff:Frontends:{name}:ClientAuthentication:KeyRing</c> as a JSON Web Key Set. A
    /// client registered with that <c>jwks_uri</c> rotates its keys without re-registration. The default is
    /// <c>false</c>. The configuration key is <c>Bff:Frontends:{name}:Jwks:Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; }
}
