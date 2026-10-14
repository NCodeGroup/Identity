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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization request after combining
/// values from the request message (query or form) and the request (JAR) object.
/// </summary>
[PublicAPI]
public interface IAuthorizationRequest
    : IBaseAuthorizationRequest,
        ISupportClone<IAuthorizationRequest>
{
    /// <summary>
    /// Gets or sets a value indicating whether this authorization request is a resumption of a previously-parked
    /// request, continued after an interactive step (login or consent) rather than received fresh from the client.
    /// </summary>
    /// <remarks>
    /// This is deliberately a settable member even though the OAuth/OIDC parameters on this interface are read-only
    /// projections unioned from the request message and the JAR request object. It is <b>not</b> a wire parameter: it is
    /// <b>server-applied runtime state</b> — set to <see langword="true"/> by the continuation provider when it resumes
    /// a parked request, rather than parsed from the request — and is serialized with the request so the flag survives
    /// the parked-continuation round-trip. The authorization endpoint reads it to avoid re-triggering an interactive
    /// step it has already performed (for example, not re-forcing <c>prompt=consent</c> on the pass that resumes right
    /// after the end-user completed the consent screen).
    /// </remarks>
    bool IsContinuation { get; set; }

    /// <summary>
    /// Gets or sets the end-user's granted subset of the requested scopes, used to down-scope the issued authorization
    /// code and tokens to exactly what was consented (OIDC down-scoping).
    /// </summary>
    /// <remarks>
    /// This is deliberately a settable member even though the OAuth/OIDC parameters on this interface are read-only
    /// projections unioned from the request message and the JAR request object. It is <b>not</b> a wire parameter: like
    /// <see cref="IsContinuation"/>, it is <b>server-applied runtime state</b> — set by the authorization endpoint after
    /// a granular consent decision rather than parsed from the request — and is serialized with the request so it
    /// survives the authorization-code round-trip to the token endpoint. It is the single down-scoping enforcement
    /// point: when set, it <b>overrides <see cref="Scopes"/></b>, so every consumer (authorization-ticket creation, the
    /// persisted authorization code, and the token-endpoint scope validation) observes only the consented scopes and a
    /// client cannot obtain a scope the end-user did not consent to. When <see langword="null"/>, no consent narrowing
    /// is applied and <see cref="Scopes"/> returns the originally-requested scopes.
    /// </remarks>
    IReadOnlyList<string>? GrantedScopes { get; set; }

    /// <summary>
    /// Gets the <see cref="IAuthorizationRequestMessage"/> which represents the OAuth request message.
    /// </summary>
    IAuthorizationRequestMessage OriginalRequestMessage { get; }

    /// <summary>
    /// Gets the <see cref="IAuthorizationRequestObject"/> which represents the OAuth request object. May be
    /// <c>null</c> when the request message didn't specify the <c>request</c> or <c>request_uri</c> parameters.
    /// </summary>
    IAuthorizationRequestObject? OriginalRequestObject { get; }

    /// <summary>
    /// Gets the <c>acr_values</c> parameter.
    /// </summary>
    IReadOnlyList<string> AcrValues { get; }

    /// <summary>
    /// Gets the <c>claims</c> parameter.
    /// </summary>
    IRequestClaims? Claims { get; }

    /// <summary>
    /// Gets the <c>claims_locales</c> parameter.
    /// </summary>
    IReadOnlyList<string> ClaimsLocales { get; }

    /// <summary>
    /// Gets the <c>client_id</c> parameter.
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// Gets the <c>code_challenge</c> parameter.
    /// </summary>
    string? CodeChallenge { get; }

    /// <summary>
    /// Gets the <c>code_challenge_method</c> parameter.
    /// </summary>
    string? CodeChallengeMethod { get; }

    /// <summary>
    /// Gets the <c>code_verifier</c> parameter.
    /// </summary>
    string? CodeVerifier { get; }

    /// <summary>
    /// Gets the <c>dpop_jkt</c> parameter (RFC 9449 §10): the JWK SHA-256 thumbprint the issued authorization code
    /// (and the tokens exchanged from it) must be bound to.
    /// </summary>
    string? DPoPJwkThumbprint { get; }

    /// <summary>
    /// Gets the <c>display</c> parameter.
    /// </summary>
    string DisplayType { get; }

    /// <summary>
    /// Gets the <c>grant_type</c> parameter.
    /// </summary>
    string GrantType { get; }

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
    /// Gets the <c>prompt_type</c> parameter.
    /// </summary>
    IReadOnlyList<string> PromptTypes { get; }

    /// <summary>
    /// Gets the <c>redirect_uri</c> parameter.
    /// </summary>
    Uri RedirectUri { get; }

    /// <summary>
    /// Gets the <c>resource</c> parameter (RFC 8707 Resource Indicators) identifying the resource servers (audiences)
    /// the issued access tokens are intended for.
    /// </summary>
    IReadOnlyList<string> Resources { get; }

    /// <summary>
    /// Gets the <c>response_mode</c> parameter.
    /// </summary>
    string ResponseMode { get; }

    /// <summary>
    /// Gets the <c>response_type</c> parameter.
    /// </summary>
    IReadOnlyList<string> ResponseTypes { get; }

    /// <summary>
    /// Gets the <c>scope</c> parameter — the scopes the request is authorized for. After a granular consent decision
    /// this is narrowed to the consented subset carried by <see cref="GrantedScopes"/> (OIDC down-scoping); it is
    /// otherwise the originally-requested scopes.
    /// </summary>
    IReadOnlyList<string> Scopes { get; }

    /// <summary>
    /// Gets the <c>state</c> parameter.
    /// </summary>
    string? State { get; }

    /// <summary>
    /// Gets the <c>ui_locales</c> parameter.
    /// </summary>
    IReadOnlyList<string> UiLocales { get; }
}
