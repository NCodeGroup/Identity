// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the protection that the BFF registered at the OpenID Provider for its ID tokens and logout tokens: the
/// algorithms they are signed and encrypted with. Bound from the <c>Bff:IdToken</c> configuration section.
/// </summary>
// See ADR-1301.
[PublicAPI]
public sealed class BffIdTokenOptions
{
    /// <summary>
    /// Gets or sets the JWS algorithms the ID tokens may be signed with, the <c>id_token_signed_response_alg</c> the BFF
    /// registered. Empty, the default, accepts any asymmetric algorithm the provider advertises. The configuration key
    /// is <c>Bff:IdToken:SigningAlgorithms</c>.
    /// </summary>
    public List<string> SigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JWE <c>alg</c> names the ID tokens are encrypted with, the
    /// <c>id_token_encrypted_response_alg</c> the BFF registered. Empty, the default, means the BFF registered no
    /// encryption, so an encrypted ID token is refused. When set, the tokens are decrypted with the keys of
    /// <c>Bff:ClientAuthentication:KeyRing</c>. The configuration key is <c>Bff:IdToken:KeyManagementAlgorithms</c>.
    /// </summary>
    public List<string> KeyManagementAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JWE <c>enc</c> names the ID tokens are encrypted with, the
    /// <c>id_token_encrypted_response_enc</c> the BFF registered. Empty, the default, means <c>A128CBC-HS256</c>. The
    /// configuration key is <c>Bff:IdToken:ContentEncryptionAlgorithms</c>.
    /// </summary>
    public List<string> ContentEncryptionAlgorithms { get; set; } = [];
}
