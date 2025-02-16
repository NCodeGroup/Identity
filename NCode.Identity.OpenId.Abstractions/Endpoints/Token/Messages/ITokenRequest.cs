#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

namespace NCode.Identity.OpenId.Endpoints.Token.Messages;

/// <summary>
/// Represents the message for an <c>OAuth</c> or <c>OpenID Connect</c> token request.
/// </summary>
[PublicAPI]
public interface ITokenRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the authorization code that was issued by the authorization server.
    /// </summary>
    string? AuthorizationCode { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the unique identifier for the <c>OAuth</c> or <c>OpenID Connect</c> client.
    /// </summary>
    string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the PKCE parameter that was used in the original authorization request.
    /// </summary>
    string? CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the type of grant that the client is requesting.
    /// </summary>
    string? GrantType { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the password of the resource owner.
    /// </summary>
    string? Password { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="Uri"/> value containing the <c>redirect_uri</c> parameter that was sent in the original authorization request.
    /// </summary>
    Uri? RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the refresh token that was issued by the authorization server.
    /// </summary>
    string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> collection containing the scopes requested by the client.
    /// </summary>
    List<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the username of the resource owner.
    /// </summary>
    string? Username { get; set; }
}
