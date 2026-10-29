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

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The encrypted part of a BFF session: the principal's claims and the tokens.
/// </summary>
internal sealed record BffSessionPayload
{
    /// <summary>
    /// Gets the claims of the session principal.
    /// </summary>
    public List<BffStoredClaim> Claims { get; init; } = [];

    /// <summary>
    /// Gets the raw ID token, used as the <c>id_token_hint</c> at logout.
    /// </summary>
    public string IdToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the refresh token, if the provider issued one.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets the cached access tokens.
    /// </summary>
    public List<BffStoredAccessToken> AccessTokens { get; init; } = [];

    /// <summary>
    /// Gets the JWK thumbprint that the session's tokens are pinned to, if DPoP is in use.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }

    /// <summary>
    /// Gets the value that a logout request must present to end the session.
    /// </summary>
    public string LogoutGuard { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the members that this version of the type does not know, so that they survive a round trip.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
