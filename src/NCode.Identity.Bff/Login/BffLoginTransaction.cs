// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// The state of an authorization request that the callback needs: kept server-side between the redirect to the provider
/// and the return of the browser.
/// </summary>
internal sealed record BffLoginTransaction
{
    /// <summary>
    /// Gets the issuer that the authorization request was sent to.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the redirect URI of the authorization request.
    /// </summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>
    /// Gets the PKCE code verifier.
    /// </summary>
    public string CodeVerifier { get; init; } = string.Empty;

    /// <summary>
    /// Gets the nonce that the ID token must carry.
    /// </summary>
    public string Nonce { get; init; } = string.Empty;

    /// <summary>
    /// Gets the local URL that the browser returns to after login.
    /// </summary>
    public string ReturnUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the JWK thumbprint of the DPoP key that the authorization request was bound to, if any.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }

    /// <summary>
    /// Gets the <c>max_age</c> that the authorization request carried, if any.
    /// </summary>
    public int? MaxAge { get; init; }

    /// <summary>
    /// Gets or sets the members that this version of the type does not know, so that they survive a round trip.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
