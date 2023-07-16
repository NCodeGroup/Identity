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
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages.Parameters;

/// <summary>
/// Contains constants for various <see cref="KnownParameters"/> used by <c>OAuth</c> and <c>OpenID Connect</c> messages.
/// </summary>
public static class KnownParameters
{
    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>acr_values</c> message parameter which parses <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> AcrValues = new(
        OpenIdConstants.Parameters.AcrValues,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.StringSet);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IRequestClaims"/> result.
    /// </summary>
    public static readonly KnownParameter<IRequestClaims?> Claims = new(
        OpenIdConstants.Parameters.Claims,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.RequestClaims);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims_locales</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> ClaimsLocales = new(
        OpenIdConstants.Parameters.ClaimsLocales,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.StringSet);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_id</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ClientId = new(
        OpenIdConstants.Parameters.ClientId,
        optional: false,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Code = new(
        OpenIdConstants.Parameters.Code,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> CodeChallenge = new(
        OpenIdConstants.Parameters.CodeChallenge,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge_method</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="CodeChallengeMethod"/> result.
    /// </summary>
    public static readonly KnownParameter<CodeChallengeMethod?> CodeChallengeMethod = new(
        OpenIdConstants.Parameters.CodeChallengeMethod,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.CodeChallengeMethod);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_verifier</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> CodeVerifier = new(
        OpenIdConstants.Parameters.CodeVerifier,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>display</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="DisplayType"/> result.
    /// </summary>
    public static readonly KnownParameter<DisplayType?> DisplayType = new(
        OpenIdConstants.Parameters.Display,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.DisplayType);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> IdToken = new(
        OpenIdConstants.Parameters.IdToken,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> IdTokenHint = new(
        OpenIdConstants.Parameters.IdTokenHint,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>access_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> AccessToken = new(
        OpenIdConstants.Parameters.AccessToken,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>token_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> TokenType = new(
        OpenIdConstants.Parameters.TokenType,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>expires_in</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> ExpiresIn = new(
        OpenIdConstants.Parameters.ExpiresIn,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.TimeSpan);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>login_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> LoginHint = new(
        OpenIdConstants.Parameters.LoginHint,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>max_age</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="TimeSpan"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> MaxAge = new(
        OpenIdConstants.Parameters.MaxAge,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.TimeSpan);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>nonce</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Nonce = new(
        OpenIdConstants.Parameters.Nonce,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>prompt</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="PromptTypes"/> result.
    /// </summary>
    public static readonly KnownParameter<PromptTypes?> PromptType = new(
        OpenIdConstants.Parameters.Prompt,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.PromptType);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>redirect_uri</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri?> RedirectUri = new(
        OpenIdConstants.Parameters.RedirectUri,
        optional: false,
        allowMultipleValues: false,
        ParameterParsers.Uri);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> RequestJwt = new(
        OpenIdConstants.Parameters.Request,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request_uri</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri?> RequestUri = new(
        OpenIdConstants.Parameters.RequestUri,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.Uri);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_mode</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="ResponseMode"/> result.
    /// </summary>
    public static readonly KnownParameter<ResponseMode?> ResponseMode = new(
        OpenIdConstants.Parameters.ResponseMode,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.ResponseMode);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="ResponseType"/> result.
    /// </summary>
    public static readonly KnownParameter<ResponseTypes?> ResponseType = new(
        OpenIdConstants.Parameters.ResponseType,
        optional: false,
        allowMultipleValues: false,
        ParameterParsers.ResponseType);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>scope</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> Scopes = new(
        OpenIdConstants.Parameters.Scope,
        optional: false,
        allowMultipleValues: true,
        ParameterParsers.StringSet);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>state</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> State = new(
        OpenIdConstants.Parameters.State,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>ui_locales</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IReadOnlyCollection{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<IReadOnlyCollection<string>?> UiLocales = new(
        OpenIdConstants.Parameters.UiLocales,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.StringSet);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>iss</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> Issuer = new(
        OpenIdConstants.Parameters.Issuer,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ErrorCode = new(
        OpenIdConstants.Parameters.ErrorCode,
        optional: false,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_description</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string?> ErrorDescription = new(
        OpenIdConstants.Parameters.ErrorDescription,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.String);

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_uri</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri?> ErrorUri = new(
        OpenIdConstants.Parameters.ErrorUri,
        optional: true,
        allowMultipleValues: false,
        ParameterParsers.Uri);
}
