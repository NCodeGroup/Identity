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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the common values used in an <c>OAuth</c> or <c>OpenID Connect</c> authorization request message.
/// </summary>
public interface IBaseAuthorizationRequestValues : IBaseAuthorizationRequest
{
    /// <summary>
    /// Gets the <c>acr_values</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? AcrValues { get; }

    /// <summary>
    /// Gets the <c>claims</c> parameter.
    /// </summary>
    IRequestClaims? Claims { get; }

    /// <summary>
    /// Gets the <c>claims_locales</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? ClaimsLocales { get; }

    /// <summary>
    /// Gets the <c>client_id</c> parameter.
    /// </summary>
    string? ClientId { get; }

    /// <summary>
    /// Gets the <c>code_challenge</c> parameter.
    /// </summary>
    string? CodeChallenge { get; }

    /// <summary>
    /// Gets the <c>code_challenge_method</c> parameter.
    /// </summary>
    CodeChallengeMethod? CodeChallengeMethod { get; }

    /// <summary>
    /// Gets the <c>code_verifier</c> parameter.
    /// </summary>
    string? CodeVerifier { get; }

    /// <summary>
    /// Gets the <c>display</c> parameter.
    /// </summary>
    DisplayType? DisplayType { get; }

    /// <summary>
    /// Gets the <c>id_token_hint</c> parameter.
    /// </summary>
    string? IdTokenHint { get; }

    /// <summary>
    /// Gets the <c>login_hint</c> parameter.
    /// </summary>
    string? LoginHint { get; }

    /// <summary>
    /// Gets the <c>max_age</c> parameter.
    /// </summary>
    TimeSpan? MaxAge { get; }

    /// <summary>
    /// Gets the <c>nonce</c> parameter.
    /// </summary>
    string? Nonce { get; }

    /// <summary>
    /// Gets the <c>prompt</c> parameter.
    /// </summary>
    PromptTypes? PromptType { get; }

    /// <summary>
    /// Gets the <c>redirect_uri</c> parameter.
    /// </summary>
    Uri? RedirectUri { get; }

    /// <summary>
    /// Gets the <c>response_mode</c> parameter.
    /// </summary>
    ResponseMode? ResponseMode { get; }

    /// <summary>
    /// Gets the <c>response_type</c> parameter.
    /// </summary>
    ResponseTypes? ResponseType { get; }

    /// <summary>
    /// Gets the <c>scope</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? Scopes { get; }

    /// <summary>
    /// Gets the <c>state</c> parameter.
    /// </summary>
    string? State { get; }

    /// <summary>
    /// Gets the <c>ui_locales</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? UiLocales { get; }
}
