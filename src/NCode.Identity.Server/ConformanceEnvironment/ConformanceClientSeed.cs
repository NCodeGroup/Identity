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
/// Describes a single <strong>conformance-only</strong> confidential client to seed: its client id, its
/// <c>Base64Url</c>-encoded secret, and the exact-match redirect URIs, grant types, token-endpoint authentication
/// methods, and PKCE code-challenge methods it registers. The scopes it may request are not part of the seed: they
/// come from the tenant's resource servers, and the OpenID Connect scopes are available to every client.
/// </summary>
[PublicAPI]
public sealed class ConformanceClientSeed
{
    /// <summary>
    /// Gets or sets the confidential client id. When empty, this entry is skipped.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret, supplied <c>Base64Url</c>-encoded (not plaintext) so a raw process-memory
    /// snapshot does not expose it directly. The decoded bytes are the credential the conformance suite presents.
    /// When empty, this entry is skipped.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the exact-match redirect URIs registered for this client (the suite's callback). When empty, this
    /// entry is skipped.
    /// </summary>
    public string[] RedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the grant types this client is allowed to use. Defaults to the Basic OP set
    /// (<c>authorization_code</c> + <c>refresh_token</c>).
    /// </summary>
    public string[] GrantTypes { get; set; } =
    [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.RefreshToken];

    /// <summary>
    /// Gets or sets the token-endpoint authentication methods this client supports. Defaults to
    /// <c>client_secret_basic</c>.
    /// </summary>
    public string[] TokenEndpointAuthMethods { get; set; } =
    [OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic];

    /// <summary>
    /// Gets or sets the PKCE code-challenge methods this client supports. Defaults to <c>S256</c> (the only
    /// recommended method; <c>plain</c> is intentionally omitted).
    /// </summary>
    public string[] CodeChallengeMethods { get; set; } =
    [OpenIdConstants.CodeChallengeMethods.S256];

    /// <summary>
    /// Gets a value indicating whether this client should be seeded.
    /// </summary>
    public bool ShouldSeed =>
        !string.IsNullOrEmpty(ClientId)
        && !string.IsNullOrEmpty(ClientSecret)
        && RedirectUris.Length > 0;
}
