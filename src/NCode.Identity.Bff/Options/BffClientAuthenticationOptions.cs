// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures how the BFF authenticates to the token endpoint of the OpenID Provider.
/// </summary>
[PublicAPI]
public sealed class BffClientAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the client authentication method, as registered by an <c>IClientAuthenticator</c>. The default is
    /// <c>private_key_jwt</c>. The configuration key is <c>Bff:Frontends:{name}:ClientAuthentication:Method</c>.
    /// </summary>
    public string Method { get; set; } = OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt;

    /// <summary>
    /// Gets or sets the name of the key ring that holds the client's authentication keys. The configuration key is
    /// <c>Bff:Frontends:{name}:ClientAuthentication:KeyRing</c>.
    /// </summary>
    public string? KeyRing { get; set; }
}
