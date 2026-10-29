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
