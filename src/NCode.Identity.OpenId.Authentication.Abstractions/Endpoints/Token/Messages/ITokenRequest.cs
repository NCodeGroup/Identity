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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;

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
    /// Gets or sets a <see cref="string"/> value containing the <c>auth_req_id</c> that the client received from the
    /// backchannel authentication endpoint and polls the token endpoint with (OpenID Connect CIBA).
    /// </summary>
    string? AuthReqId { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the unique identifier for the <c>OAuth</c> or <c>OpenID Connect</c> client.
    /// </summary>
    string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the PKCE parameter that was used in the original authorization request.
    /// </summary>
    string? CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the <c>device_code</c> that the device received from the
    /// device authorization endpoint and polls the token endpoint with (RFC 8628).
    /// </summary>
    string? DeviceCode { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the type of grant that the client is requesting.
    /// </summary>
    string? GrantType { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the password of the resource owner.
    /// </summary>
    string? Password { get; set; }

    /// <summary>
    /// Gets or sets the <c>redirect_uri</c> parameter as written, which must be identical to the one in the authorization request.
    /// </summary>
    string? RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the refresh token that was issued by the authorization server.
    /// </summary>
    string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> collection containing the scopes requested by the client.
    /// </summary>
    List<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> collection containing the <c>resource</c> parameter (RFC 8707 Resource
    /// Indicators) identifying the resource servers (audiences) the issued access token is intended for.
    /// </summary>
    List<string>? Resources { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> collection containing the <c>audience</c> parameter (RFC 8693 §2.1)
    /// identifying the logical target services the issued token is intended for, used to narrow the token's audience
    /// during a token exchange.
    /// </summary>
    List<string>? Audience { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the <c>subject_token</c> parameter (RFC 8693 §2.1): the
    /// security token that represents the identity of the party on behalf of whom the exchange is being made.
    /// </summary>
    string? SubjectToken { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the <c>subject_token_type</c> parameter (RFC 8693 §2.1):
    /// the token type identifier of the <c>subject_token</c>.
    /// </summary>
    string? SubjectTokenType { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the <c>actor_token</c> parameter (RFC 8693 §2.1): the
    /// security token that represents the identity of the acting party, present only for delegation.
    /// </summary>
    string? ActorToken { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the <c>actor_token_type</c> parameter (RFC 8693 §2.1): the
    /// token type identifier of the <c>actor_token</c>.
    /// </summary>
    string? ActorTokenType { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the <c>requested_token_type</c> parameter (RFC 8693 §2.1):
    /// the token type identifier the client is requesting for the issued token.
    /// </summary>
    string? RequestedTokenType { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="string"/> value containing the username of the resource owner.
    /// </summary>
    string? Username { get; set; }
}
