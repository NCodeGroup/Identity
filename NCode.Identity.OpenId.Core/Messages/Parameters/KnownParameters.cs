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

using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Messages.Parameters;

// TODO: revisit the `Optional` argument. It really should be `AllowMissingValues` or something like that.

/// <summary>
/// Contains constants for various <see cref="KnownParameters"/> used by <c>OAuth</c> and <c>OpenID Connect</c> messages.
/// </summary>
public static class KnownParameters
{
    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>acr_values</c> message parameter which parses <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> AcrValues =
        new(OpenIdConstants.Parameters.AcrValues, ParameterParsers.StringSet)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IRequestClaims"/> result.
    /// </summary>
    public static readonly KnownParameter<IRequestClaims?> Claims =
        new(OpenIdConstants.Parameters.Claims, ParameterParsers.RequestClaims)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims_locales</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> ClaimsLocales =
        new(OpenIdConstants.Parameters.ClaimsLocales, ParameterParsers.StringSet)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_id</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ClientId =
        new(OpenIdConstants.Parameters.ClientId, ParameterParsers.String)
        {
            Optional = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_secret</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ClientSecret =
        new(OpenIdConstants.Parameters.ClientSecret, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> AuthorizationCode =
        new(OpenIdConstants.Parameters.AuthorizationCode, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> CodeChallenge =
        new(OpenIdConstants.Parameters.CodeChallenge, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge_method</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> CodeChallengeMethod =
        new(OpenIdConstants.Parameters.CodeChallengeMethod, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_verifier</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> CodeVerifier =
        new(OpenIdConstants.Parameters.CodeVerifier, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>display</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> DisplayType =
        new(OpenIdConstants.Parameters.Display, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>grant_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> GrantType =
        new(OpenIdConstants.Parameters.GrantType, ParameterParsers.String)
        {
            Optional = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> IdToken =
        new(OpenIdConstants.Parameters.IdToken, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> IdTokenHint =
        new(OpenIdConstants.Parameters.IdTokenHint, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>access_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> AccessToken =
        new(OpenIdConstants.Parameters.AccessToken, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>token_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> TokenType =
        new(OpenIdConstants.Parameters.TokenType, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>expires_in</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> ExpiresIn =
        new(OpenIdConstants.Parameters.ExpiresIn, ParameterParsers.TimeSpan)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>login_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> LoginHint =
        new(OpenIdConstants.Parameters.LoginHint, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>max_age</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="TimeSpan"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> MaxAge =
        new(OpenIdConstants.Parameters.MaxAge, ParameterParsers.TimeSpan)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>nonce</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Nonce =
        new(OpenIdConstants.Parameters.Nonce, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>password</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Password =
        new(OpenIdConstants.Parameters.Password, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>prompt</c> message parameter which parses <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> PromptType =
        new(OpenIdConstants.Parameters.Prompt, ParameterParsers.StringSet)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>redirect_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri?> RedirectUri =
        new(OpenIdConstants.Parameters.RedirectUri, ParameterParsers.Uri)
        {
            Optional = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>refresh_token</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> RefreshToken =
        new(OpenIdConstants.Parameters.RefreshToken, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> RequestJwt =
        new(OpenIdConstants.Parameters.Request, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri?> RequestUri =
        new(OpenIdConstants.Parameters.RequestUri, ParameterParsers.Uri)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_mode</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ResponseMode =
        new(OpenIdConstants.Parameters.ResponseMode, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_type</c> message parameter which parses <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> ResponseTypes =
        new(OpenIdConstants.Parameters.ResponseType, ParameterParsers.StringSet)
        {
            Optional = false,
            SortStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>scope</c> message parameter which parses <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> Scopes =
        new(OpenIdConstants.Parameters.Scope, ParameterParsers.StringSet)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>state</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> State =
        new(OpenIdConstants.Parameters.State, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>ui_locales</c> message parameter which parses <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> UiLocales =
        new(OpenIdConstants.Parameters.UiLocales, ParameterParsers.StringSet)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>username</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Username =
        new(OpenIdConstants.Parameters.Username, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>iss</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Issuer =
        new(OpenIdConstants.Parameters.IssuerShort, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ErrorCode =
        new(OpenIdConstants.Parameters.ErrorCode, ParameterParsers.String)
        {
            Optional = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_description</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ErrorDescription =
        new(OpenIdConstants.Parameters.ErrorDescription, ParameterParsers.String)
        {
            Optional = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri?> ErrorUri =
        new(OpenIdConstants.Parameters.ErrorUri, ParameterParsers.Uri)
        {
            Optional = true,
        };
}
