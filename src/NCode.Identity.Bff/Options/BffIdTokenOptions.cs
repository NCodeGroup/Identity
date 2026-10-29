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

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the protection that the BFF registered at the OpenID Provider for its ID tokens and logout tokens: the
/// algorithms they are signed and encrypted with. Bound from the <c>Bff:IdToken</c> configuration section.
/// </summary>
// See ADR-0301.
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
