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
/// Configures whether the BFF loads UserInfo at login and the protection it registered at the OpenID Provider for the
/// UserInfo responses: the algorithms they are signed and encrypted with. Bound from the <c>Bff:UserInfo</c>
/// configuration section.
/// </summary>
// See ADR-0299 and ADR-0301.
[PublicAPI]
public sealed class BffUserInfoOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the BFF requests the provider's UserInfo endpoint at login and adds its
    /// claims to the session, for providers that keep profile claims out of the ID token. A UserInfo claim replaces the
    /// ID token's claims of its type, except the ones that anchor the session to the authentication. The default is
    /// <c>false</c>. The configuration key is <c>Bff:UserInfo:Load</c>.
    /// </summary>
    public bool Load { get; set; }

    /// <summary>
    /// Gets or sets the JWS algorithms the UserInfo responses are signed with, the <c>userinfo_signed_response_alg</c>
    /// the BFF registered. Empty, the default, means the BFF registered no UserInfo signing, so the response must be
    /// JSON; when set, it must be a JWT signed with one of them. The configuration key is
    /// <c>Bff:UserInfo:SigningAlgorithms</c>.
    /// </summary>
    public List<string> SigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JWE <c>alg</c> names the UserInfo responses are encrypted with, the
    /// <c>userinfo_encrypted_response_alg</c> the BFF registered. Empty, the default, means the BFF registered no
    /// UserInfo encryption. It requires <see cref="SigningAlgorithms"/>, because a response that is encrypted but not
    /// signed is refused, and the responses are decrypted with the keys of <c>Bff:ClientAuthentication:KeyRing</c>. The
    /// configuration key is <c>Bff:UserInfo:KeyManagementAlgorithms</c>.
    /// </summary>
    public List<string> KeyManagementAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets or sets the JWE <c>enc</c> names the UserInfo responses are encrypted with, the
    /// <c>userinfo_encrypted_response_enc</c> the BFF registered. Empty, the default, means <c>A128CBC-HS256</c>. The
    /// configuration key is <c>Bff:UserInfo:ContentEncryptionAlgorithms</c>.
    /// </summary>
    public List<string> ContentEncryptionAlgorithms { get; set; } = [];
}
