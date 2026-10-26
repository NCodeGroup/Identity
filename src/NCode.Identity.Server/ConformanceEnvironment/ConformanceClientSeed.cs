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
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.ConformanceEnvironment;

/// <summary>
/// Describes a single <strong>conformance-only</strong> confidential client to seed: its credential —
/// a <c>Base64Url</c>-encoded secret, the public keys of a JSON Web Key Set, or both — and the exact-match redirect
/// URIs, grant types, token-endpoint authentication method, PKCE code-challenge methods, and security profiles it
/// registers. The client id is its key in <see cref="ConformanceSeedOptions.Clients"/>. The scopes it may request
/// are not part of the seed: they
/// come from the tenant's resource servers, and the OpenID Connect scopes are available to every client.
/// </summary>
/// <remarks>
/// The list options start empty and their defaults apply only when none are configured, because the configuration
/// binder appends the configured values to an array that already holds some.
/// </remarks>
[PublicAPI]
public sealed class ConformanceClientSeed
{
    /// <summary>
    /// Gets or sets the client secret, supplied <c>Base64Url</c>-encoded (not plaintext) so a raw process-memory
    /// snapshot does not expose it directly. The decoded bytes are the credential the conformance suite presents.
    /// A client with neither a secret nor <see cref="Jwks"/> is skipped.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the JSON text of the JSON Web Key Set whose public keys the client registers by value, imported
    /// under the same client-key policy as a dynamically registered <c>jwks</c>. The keys verify the client's
    /// assertions and request objects, and a key carrying an <c>x5c</c> certificate is the one a
    /// <c>self_signed_tls_client_auth</c> client presents. A client with neither a secret nor a key set is skipped.
    /// </summary>
    public string? Jwks { get; set; }

    /// <summary>
    /// Gets or sets the exact-match redirect URIs registered for this client (the suite's callback). When empty, this
    /// entry is skipped.
    /// </summary>
    public string[] RedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the grant types this client is allowed to use. When none are configured, the client uses the Basic
    /// OP set (<c>authorization_code</c> + <c>refresh_token</c>).
    /// </summary>
    public string[] GrantTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the token-endpoint authentication method the client registers (RFC 7591 §2), which decides whether
    /// it is a confidential client, and which it is the only method permitted to use. Defaults to
    /// <c>client_secret_basic</c>.
    /// </summary>
    public string TokenEndpointAuthMethod { get; set; } =
        OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic;

    /// <summary>
    /// Gets or sets the PKCE code-challenge methods this client supports. When none are configured, the client uses
    /// the methods the server supports, which are <c>S256</c> by default.
    /// </summary>
    public string[] CodeChallengeMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets the security profiles the client selects, such as <c>fapi1-advanced</c>. When none are configured,
    /// the client selects none and takes the tenant's.
    /// </summary>
    public string[] SecurityProfiles { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether this client should be seeded: it has a redirect URI and a credential.
    /// </summary>
    public bool ShouldSeed =>
        (!string.IsNullOrEmpty(ClientSecret) || !string.IsNullOrEmpty(Jwks))
        && RedirectUris.Length > 0;
}
