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
/// Contains constants for various <see cref="CoreParameters"/> used by <c>OAuth</c> and <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public static class CoreParameters
{
    /// <summary>
    /// A reusable delegate that indicates that a parameter should only be serialized when the format is <see cref="SerializationFormat.Json"/>.
    /// </summary>
    public static bool ShouldSerializeAsJsonOnly(
        OpenIdEnvironment openIdEnvironment,
        IParameter parameter,
        SerializationFormat format
    ) =>
        format == SerializationFormat.Json;

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>acr_values</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> AcrValues =
        new(OpenIdConstants.Parameters.AcrValues, CoreParameterParsers.StringList)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>$authorization_source_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="string"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AuthorizationSourceType =
        new(OpenIdConstants.Parameters.AuthorizationSourceType, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
            ShouldSerialize = ShouldSerializeAsJsonOnly,
        };

    // TODO
    // /// <summary>
    // /// Gets the <see cref="KnownParameter"/> for the <c>claims</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IRequestClaims"/> result.
    // /// </summary>
    // public static readonly KnownParameter<IRequestClaims> Claims =
    //     new(OpenIdConstants.Parameters.Claims, ParameterParsers.RequestClaims)
    //     {
    //         AllowMissingStringValues = true,
    //     };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims_locales</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> ClaimsLocales =
        new(OpenIdConstants.Parameters.ClaimsLocales, CoreParameterParsers.StringList)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_id</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ClientId =
        new(OpenIdConstants.Parameters.ClientId, CoreParameterParsers.String)
        {
            AllowMissingStringValues = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>client_secret</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ClientSecret =
        new(OpenIdConstants.Parameters.ClientSecret, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>$created_when</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="DateTimeOffset"/> result.
    /// </summary>
    public static readonly KnownParameter<DateTimeOffset> CreatedWhen =
        new(OpenIdConstants.Parameters.CreatedWhen, CoreParameterParsers.DateTimeOffset)
        {
            AllowMissingStringValues = true,
            ShouldSerialize = ShouldSerializeAsJsonOnly,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AuthorizationCode =
        new(OpenIdConstants.Parameters.AuthorizationCode, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> CodeChallenge =
        new(OpenIdConstants.Parameters.CodeChallenge, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_challenge_method</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> CodeChallengeMethod =
        new(OpenIdConstants.Parameters.CodeChallengeMethod, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>code_verifier</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> CodeVerifier =
        new(OpenIdConstants.Parameters.CodeVerifier, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>display</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> DisplayType =
        new(OpenIdConstants.Parameters.Display, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>grant_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> GrantType =
        new(OpenIdConstants.Parameters.GrantType, CoreParameterParsers.String)
        {
            AllowMissingStringValues = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> IdToken =
        new(OpenIdConstants.Parameters.IdToken, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>id_token_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> IdTokenHint =
        new(OpenIdConstants.Parameters.IdTokenHint, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>access_token</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> AccessToken =
        new(OpenIdConstants.Parameters.AccessToken, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>token_type</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> TokenType =
        new(OpenIdConstants.Parameters.TokenType, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>expires_in</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> ExpiresIn =
        new(OpenIdConstants.Parameters.ExpiresIn, CoreParameterParsers.TimeSpan.AsNullableValue())
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>login_hint</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> LoginHint =
        new(OpenIdConstants.Parameters.LoginHint, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>max_age</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="TimeSpan"/> result.
    /// </summary>
    public static readonly KnownParameter<TimeSpan?> MaxAge =
        new(OpenIdConstants.Parameters.MaxAge, CoreParameterParsers.TimeSpan.AsNullableValue())
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>nonce</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Nonce =
        new(OpenIdConstants.Parameters.Nonce, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>password</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Password =
        new(OpenIdConstants.Parameters.Password, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>prompt</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> PromptType =
        new(OpenIdConstants.Parameters.Prompt, CoreParameterParsers.StringList)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>redirect_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri> RedirectUri =
        new(OpenIdConstants.Parameters.RedirectUri, CoreParameterParsers.Uri)
        {
            AllowMissingStringValues = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>refresh_token</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> RefreshToken =
        new(OpenIdConstants.Parameters.RefreshToken, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> RequestJwt =
        new(OpenIdConstants.Parameters.Request, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    // TODO
    // /// <summary>
    // /// Gets the <see cref="KnownParameter"/> for the <c>$request_object_source</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="RequestObjectSource"/> result.
    // /// </summary>
    // public static readonly KnownParameter<RequestObjectSource> RequestObjectSource =
    //     new(OpenIdConstants.Parameters.RequestObjectSource, EnumParser<RequestObjectSource>.Singleton)
    //     {
    //         AllowMissingStringValues = true,
    //         ShouldSerialize = ShouldSerializeAsJsonOnly,
    //     };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>request_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="Uri"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri> RequestUri =
        new(OpenIdConstants.Parameters.RequestUri, CoreParameterParsers.Uri)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_mode</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ResponseMode =
        new(OpenIdConstants.Parameters.ResponseMode, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>response_type</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> ResponseTypes =
        new(OpenIdConstants.Parameters.ResponseType, CoreParameterParsers.StringList)
        {
            AllowMissingStringValues = false,
            SortStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>scope</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> Scopes =
        new(OpenIdConstants.Parameters.Scope, CoreParameterParsers.StringList)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>state</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> State =
        new(OpenIdConstants.Parameters.State, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>ui_locales</c> message parameter which parses <see cref="StringValues"/> into an <see cref="List{String}"/> result.
    /// </summary>
    public static readonly KnownParameter<List<string>> UiLocales =
        new(OpenIdConstants.Parameters.UiLocales, CoreParameterParsers.StringList)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>username</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Username =
        new(OpenIdConstants.Parameters.Username, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>iss</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> Issuer =
        new(OpenIdConstants.Parameters.IssuerShort, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ErrorCode =
        new(OpenIdConstants.Parameters.ErrorCode, CoreParameterParsers.String)
        {
            AllowMissingStringValues = false,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_description</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<string> ErrorDescription =
        new(OpenIdConstants.Parameters.ErrorDescription, CoreParameterParsers.String)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>error_uri</c> message parameter which parses <see cref="StringValues"/> into an <see cref="String"/> result.
    /// </summary>
    public static readonly KnownParameter<Uri> ErrorUri =
        new(OpenIdConstants.Parameters.ErrorUri, CoreParameterParsers.Uri)
        {
            AllowMissingStringValues = true,
        };
}
