// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the common values used in an <c>OAuth</c> or <c>OpenID Connect</c> authorization request message.
/// </summary>
[PublicAPI]
public interface IBaseAuthorizationRequestValues : IBaseAuthorizationRequest
{
    /// <summary>
    /// Gets or sets the <c>acr_values</c> parameter.
    /// </summary>
    List<string>? AcrValues { get; set; }

    /// <summary>
    /// Gets or sets the <c>claims</c> parameter.
    /// </summary>
    IRequestClaims? Claims { get; set; }

    /// <summary>
    /// Gets or sets the <c>claims_locales</c> parameter.
    /// </summary>
    List<string>? ClaimsLocales { get; set; }

    /// <summary>
    /// Gets or sets the <c>client_id</c> parameter.
    /// </summary>
    string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the <c>code_challenge</c> parameter.
    /// </summary>
    string? CodeChallenge { get; set; }

    /// <summary>
    /// Gets or sets the <c>code_challenge_method</c> parameter.
    /// </summary>
    string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// Gets or sets the <c>code_verifier</c> parameter.
    /// </summary>
    string? CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets the <c>dpop_jkt</c> parameter (RFC 9449 §10).
    /// </summary>
    string? DPoPJwkThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the <c>display</c> parameter.
    /// </summary>
    string? DisplayType { get; set; }

    /// <summary>
    /// Gets or sets the <c>id_token_hint</c> parameter.
    /// </summary>
    string? IdTokenHint { get; set; }

    /// <summary>
    /// Gets or sets the <c>login_hint</c> parameter.
    /// </summary>
    string? LoginHint { get; set; }

    /// <summary>
    /// Gets or sets the <c>max_age</c> parameter.
    /// </summary>
    TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the <c>nonce</c> parameter.
    /// </summary>
    string? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the <c>prompt</c> parameter.
    /// </summary>
    List<string>? PromptTypes { get; set; }

    /// <summary>
    /// Gets or sets the <c>redirect_uri</c> parameter.
    /// </summary>
    string? RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the <c>resource</c> parameter (RFC 8707 Resource Indicators) identifying the resource servers
    /// (audiences) the issued access tokens are intended for.
    /// </summary>
    List<string>? Resources { get; set; }

    /// <summary>
    /// Gets or sets the <c>response_mode</c> parameter.
    /// </summary>
    string? ResponseMode { get; set; }

    /// <summary>
    /// Gets or sets the <c>response_type</c> parameter.
    /// </summary>
    List<string>? ResponseTypes { get; set; }

    /// <summary>
    /// Gets or sets the <c>scope</c> parameter.
    /// </summary>
    List<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }

    /// <summary>
    /// Gets or sets the <c>ui_locales</c> parameter.
    /// </summary>
    List<string>? UiLocales { get; set; }
}
