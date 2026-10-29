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

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// Represents a refresh token that the authorization server issued, with the moment its rotation family expires.
/// </summary>
// See ADR-0297.
[PublicAPI]
public readonly record struct IssuedRefreshToken
{
    /// <summary>
    /// Gets the issued refresh token. Its <see cref="SecurityToken.TokenLifetime"/> ends when the token itself expires,
    /// which under the <c>sliding</c> expiration policy can be before its family does.
    /// </summary>
    public required SecurityToken Token { get; init; }

    /// <summary>
    /// Gets when the token's rotation family expires, however often its tokens are used or rotated: the fixed horizon
    /// that a token response announces as <c>refresh_token_expires_in</c>.
    /// </summary>
    public required DateTimeOffset LineageExpiresWhen { get; init; }
}
