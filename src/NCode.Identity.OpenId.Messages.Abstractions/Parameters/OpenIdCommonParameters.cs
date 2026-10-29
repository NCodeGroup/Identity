#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Contains constants for various <see cref="OpenIdCommonParameters"/> used by <c>OAuth</c> and <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public static class OpenIdCommonParameters
{
    /// <summary>
    /// A reusable delegate that indicates that a parameter should only be serialized when the format is <see cref="SerializationFormat.Json"/>.
    /// </summary>
    public static bool ShouldSerializeAsJsonOnly(
        OpenIdEnvironment openIdEnvironment,
        IParameter parameter,
        SerializationFormat format
    ) => format == SerializationFormat.Json;

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>acr_values</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> AcrValues = new(
        OpenIdProtocol.Parameters.AcrValues,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>$authorization_source_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="string"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AuthorizationSourceType = new(
        OpenIdConstants.InternalParameters.AuthorizationSourceType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
        ShouldSerialize = ShouldSerializeAsJsonOnly,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims_locales</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> ClaimsLocales = new(
        OpenIdProtocol.Parameters.ClaimsLocales,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_id</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ClientId = new(
        OpenIdProtocol.Parameters.ClientId,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = false,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_secret</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ClientSecret = new(
        OpenIdProtocol.Parameters.ClientSecret,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>$created_when</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="DateTimeOffset"/> result.
    /// </summary>
    public static readonly KnownParameter<DateTimeOffset> CreatedWhen = new(
        OpenIdConstants.InternalParameters.CreatedWhen,
        CommonParameterParsers.DateTimeOffset
    )
    {
        AllowMissingStringValues = true,
        ShouldSerialize = ShouldSerializeAsJsonOnly,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AuthorizationCode = new(
        OpenIdProtocol.Parameters.AuthorizationCode,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> CodeChallenge = new(
        OpenIdProtocol.Parameters.CodeChallenge,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge_method</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> CodeChallengeMethod = new(
        OpenIdProtocol.Parameters.CodeChallengeMethod,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_verifier</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> CodeVerifier = new(
        OpenIdProtocol.Parameters.CodeVerifier,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>dpop_jkt</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> DPoPJwkThumbprint = new(
        OpenIdProtocol.Parameters.DPoPJwkThumbprint,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>display</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> DisplayType = new(
        OpenIdProtocol.Parameters.Display,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>grant_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> GrantType = new(
        OpenIdProtocol.Parameters.GrantType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = false,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> IdToken = new(
        OpenIdProtocol.Parameters.IdToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> IdTokenHint = new(
        OpenIdProtocol.Parameters.IdTokenHint,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>access_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AccessToken = new(
        OpenIdProtocol.Parameters.AccessToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>token_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> TokenType = new(
        OpenIdProtocol.Parameters.TokenType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>expires_in</c> message parameter, carried as an integer number
    /// of seconds (a JSON number in the token response per OAuth 2.0 §5.1).
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> ExpiresIn = new(
        OpenIdProtocol.Parameters.ExpiresIn,
        CommonParameterParsers.DurationSeconds
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the de-facto <c>refresh_token_expires_in</c> token response member,
    /// carried as an integer number of seconds like <c>expires_in</c>.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> RefreshTokenExpiresIn = new(
        OpenIdProtocol.Parameters.RefreshTokenExpiresIn,
        CommonParameterParsers.DurationSeconds
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>login_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> LoginHint = new(
        OpenIdProtocol.Parameters.LoginHint,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>max_age</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="TimeSpan"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> MaxAge = new(
        OpenIdProtocol.Parameters.MaxAge,
        CommonParameterParsers.TimeSpan.AsNullableValue()
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>nonce</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Nonce = new(
        OpenIdProtocol.Parameters.Nonce,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>password</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Password = new(
        OpenIdProtocol.Parameters.Password,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>prompt</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> PromptType = new(
        OpenIdProtocol.Parameters.Prompt,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>redirect_uri</c> message parameter, kept as the <see cref="String"/> the client wrote, because a redirect URI is matched, delivered, and compared as written.
    /// </summary>
    public static readonly KnownParameter<string> RedirectUri = new(
        OpenIdProtocol.Parameters.RedirectUri,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = false,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>device_code</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> DeviceCode = new(
        OpenIdProtocol.Parameters.DeviceCode,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>auth_req_id</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AuthReqId = new(
        OpenIdProtocol.Parameters.AuthReqId,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>login_hint_token</c> message parameter (OpenID Connect CIBA).
    /// </summary>
    public static readonly KnownParameter<string> LoginHintToken = new(
        OpenIdProtocol.Parameters.LoginHintToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>binding_message</c> message parameter (OpenID Connect CIBA).
    /// </summary>
    public static readonly KnownParameter<string> BindingMessage = new(
        OpenIdProtocol.Parameters.BindingMessage,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_notification_token</c> message parameter (OpenID Connect CIBA).
    /// </summary>
    public static readonly KnownParameter<string> ClientNotificationToken = new(
        OpenIdProtocol.Parameters.ClientNotificationToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>requested_expiry</c> message parameter (OpenID Connect CIBA).
    /// </summary>
    public static readonly KnownParameter<string> RequestedExpiry = new(
        OpenIdProtocol.Parameters.RequestedExpiry,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>refresh_token</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> RefreshToken = new(
        OpenIdProtocol.Parameters.RefreshToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>token</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Token = new(
        OpenIdProtocol.Parameters.Token,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>token_type_hint</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> TokenTypeHint = new(
        OpenIdProtocol.Parameters.TokenTypeHint,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> RequestJwt = new(
        OpenIdProtocol.Parameters.Request,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri> RequestUri = new(
        OpenIdProtocol.Parameters.RequestUri,
        CommonParameterParsers.Uri
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_mode</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ResponseMode = new(
        OpenIdProtocol.Parameters.ResponseMode,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_type</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> ResponseTypes = new(
        OpenIdProtocol.Parameters.ResponseType,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = false,
        SortStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>resource</c> message parameter (RFC 8707 Resource Indicators)
    /// which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> Resources = new(
        OpenIdProtocol.Parameters.Resource,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>scope</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> Scopes = new(
        OpenIdProtocol.Parameters.Scope,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>audience</c> message parameter (RFC 8693 §2.1) which parses
    /// <see cref="StringValues"/> into a <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> Audience = new(
        OpenIdProtocol.Parameters.Audience,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>subject_token</c> message parameter (RFC 8693 §2.1) which
    /// parses <see cref="StringValues"/> into a <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> SubjectToken = new(
        OpenIdProtocol.Parameters.SubjectToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>subject_token_type</c> message parameter (RFC 8693 §2.1) which
    /// parses <see cref="StringValues"/> into a <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> SubjectTokenType = new(
        OpenIdProtocol.Parameters.SubjectTokenType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>actor_token</c> message parameter (RFC 8693 §2.1) which parses
    /// <see cref="StringValues"/> into a <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ActorToken = new(
        OpenIdProtocol.Parameters.ActorToken,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>actor_token_type</c> message parameter (RFC 8693 §2.1) which
    /// parses <see cref="StringValues"/> into a <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ActorTokenType = new(
        OpenIdProtocol.Parameters.ActorTokenType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>requested_token_type</c> message parameter (RFC 8693 §2.1)
    /// which parses <see cref="StringValues"/> into a <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> RequestedTokenType = new(
        OpenIdProtocol.Parameters.RequestedTokenType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>issued_token_type</c> response parameter (RFC 8693 §2.2.1)
    /// which parses <see cref="StringValues"/> into a <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> IssuedTokenType = new(
        OpenIdProtocol.Parameters.IssuedTokenType,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>state</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> State = new(
        OpenIdProtocol.Parameters.State,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>session_state</c> message parameter (OpenID Connect Session
    /// Management 1.0) which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> SessionState = new(
        OpenIdProtocol.Parameters.SessionState,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>post_logout_redirect_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> PostLogoutRedirectUri = new(
        OpenIdProtocol.Parameters.PostLogoutRedirectUri,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>ui_locales</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> UiLocales = new(
        OpenIdProtocol.Parameters.UiLocales,
        CommonParameterParsers.StringList
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>username</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Username = new(
        OpenIdProtocol.Parameters.Username,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>iss</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Issuer = new(
        OpenIdProtocol.Parameters.IssuerShort,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ErrorCode = new(
        OpenIdProtocol.Parameters.ErrorCode,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = false,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_description</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ErrorDescription = new(
        OpenIdProtocol.Parameters.ErrorDescription,
        CommonParameterParsers.String
    )
    {
        AllowMissingStringValues = true,
    };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri> ErrorUri = new(
        OpenIdProtocol.Parameters.ErrorUri,
        CommonParameterParsers.Uri
    )
    {
        AllowMissingStringValues = true,
    };
}
