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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the common values used in an <c>OAuth</c> or <c>OpenID Connect</c> authorization request message.
/// </summary>
[PublicAPI]
public interface IBaseAuthorizationRequestValues : IBaseAuthorizationRequest
{
    /// <summary>
    /// Gets or sets the <c>acr_values</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? AcrValues { get; set; }

    /// <summary>
    /// Gets or sets the <c>claims</c> parameter.
    /// </summary>
    IRequestClaims? Claims { get; set; }

    /// <summary>
    /// Gets or sets the <c>claims_locales</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? ClaimsLocales { get; set; }

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
    IReadOnlyCollection<string>? PromptTypes { get; set; }

    /// <summary>
    /// Gets or sets the <c>redirect_uri</c> parameter.
    /// </summary>
    Uri? RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the <c>response_mode</c> parameter.
    /// </summary>
    string? ResponseMode { get; set; }

    /// <summary>
    /// Gets or sets the <c>response_type</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? ResponseTypes { get; set; }

    /// <summary>
    /// Gets or sets the <c>scope</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }

    /// <summary>
    /// Gets or sets the <c>ui_locales</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? UiLocales { get; set; }
}
