#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;

/// <summary>
/// Represents the message for an <c>OAuth</c> or <c>OpenID Connect</c> token response.
/// </summary>
[PublicAPI]
public interface ITokenResponse : IOpenIdResponse
{
    /// <summary>
    /// Gets or sets the <c>access_token</c> parameter.
    /// </summary>
    string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>expires_in</c> parameter.
    /// </summary>
    TimeSpan? ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the <c>id_token</c> parameter.
    /// </summary>
    string? IdToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>refresh_token</c> parameter.
    /// </summary>
    string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the de-facto <c>refresh_token_expires_in</c> parameter: the time until the refresh token's rotation
    /// family expires, however often its tokens are used or rotated. It never grows within a family, so a client may
    /// bound a session by it.
    /// </summary>
    // See ADR-0297.
    TimeSpan? RefreshTokenExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the <c>scope</c> parameter.
    /// </summary>
    List<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the <c>token_type</c> parameter.
    /// </summary>
    string? TokenType { get; set; }

    /// <summary>
    /// Gets or sets the <c>issued_token_type</c> parameter (RFC 8693 §2.2.1): the token type identifier of the token
    /// issued in the <see cref="AccessToken"/> field by an OAuth 2.0 Token Exchange.
    /// </summary>
    string? IssuedTokenType { get; set; }
}
