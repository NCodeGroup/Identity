// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures whether the BFF loads UserInfo at login and the protection it registered at the OpenID Provider for the
/// UserInfo responses: the algorithms they are signed and encrypted with. Bound from the
/// <c>Bff:Frontends:{name}:UserInfo</c> configuration section.
/// </summary>
// See ADR-1299 and ADR-1301.
[PublicAPI]
public sealed class BffUserInfoOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the BFF requests the provider's UserInfo endpoint at login and adds its
    /// claims to the session, for providers that keep profile claims out of the ID token. A UserInfo claim replaces the
    /// ID token's claims of its type, except the ones that anchor the session to the authentication. The default is
    /// <c>false</c>. The configuration key is <c>Bff:Frontends:{name}:UserInfo:Load</c>.
    /// </summary>
    public bool Load { get; set; }

    /// <summary>
    /// Gets or sets the JWS algorithms the UserInfo responses are signed with, the <c>userinfo_signed_response_alg</c>
    /// the BFF registered. Empty, the default, means the BFF registered no UserInfo signing, so the response must be
    /// JSON; when set, it must be a JWT signed with one of them. The configuration key is
    /// <c>Bff:Frontends:{name}:UserInfo:SigningAlgorithms</c>.
    /// </summary>
    public List<string> SigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JWE <c>alg</c> names the UserInfo responses are encrypted with, the
    /// <c>userinfo_encrypted_response_alg</c> the BFF registered. Empty, the default, means the BFF registered no
    /// UserInfo encryption. It requires <see cref="SigningAlgorithms"/>, because a response that is encrypted but not
    /// signed is refused, and the responses are decrypted with the keys of
    /// <c>Bff:Frontends:{name}:ClientAuthentication:KeyRing</c>. The configuration key is
    /// <c>Bff:Frontends:{name}:UserInfo:KeyManagementAlgorithms</c>.
    /// </summary>
    public List<string> KeyManagementAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JWE <c>enc</c> names the UserInfo responses are encrypted with, the
    /// <c>userinfo_encrypted_response_enc</c> the BFF registered. Empty, the default, means <c>A128CBC-HS256</c>. The
    /// configuration key is <c>Bff:Frontends:{name}:UserInfo:ContentEncryptionAlgorithms</c>.
    /// </summary>
    public List<string> ContentEncryptionAlgorithms { get; set; } = [];
}
