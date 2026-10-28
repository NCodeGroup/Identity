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

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationRequest"/> abstraction.
/// </summary>
public sealed class AuthorizationRequest : IAuthorizationRequest
{
    private IParameterCollection? ParametersOrNull { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequest"/> class.
    /// </summary>
    /// <param name="isContinuation">Indicates whether this request is a continuation of a previous request.</param>
    /// <param name="requestMessage">The original request message.</param>
    /// <param name="requestObject">The original request object.</param>
    public AuthorizationRequest(
        bool isContinuation,
        IAuthorizationRequestMessage requestMessage,
        IAuthorizationRequestObject? requestObject
    )
    {
        IsContinuation = isContinuation;
        OriginalRequestMessage = requestMessage;
        OriginalRequestObject = requestObject;
    }

    /// <summary>
    /// Initializes a new instance of the current class that is a clone of another instance.
    /// </summary>
    /// <param name="other">The other instance to clone.</param>
    private AuthorizationRequest(AuthorizationRequest other)
    {
        IsContinuation = other.IsContinuation;
        GrantedScopes = other.GrantedScopes;
        OriginalRequestMessage = other.OriginalRequestMessage.Clone();
        OriginalRequestObject = other.OriginalRequestObject?.Clone();
    }

    /// <inheritdoc />
    public string AuthorizationSourceType => AuthorizationSourceTypes.Union;

    /// <inheritdoc />
    public OpenIdEnvironment OpenIdEnvironment => OriginalRequestMessage.OpenIdEnvironment;

    /// <inheritdoc />
    public IParameterCollection Parameters => ParametersOrNull ??= ComposeParameters();

    private IParameterCollection ComposeParameters() =>
        OriginalRequestObject?.Parameters ?? OriginalRequestMessage.Parameters;

    /// <inheritdoc />
    public bool IsContinuation { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<string>? GrantedScopes { get; set; }

    /// <inheritdoc />
    public IAuthorizationRequestMessage OriginalRequestMessage { get; }

    /// <inheritdoc />
    public IAuthorizationRequestObject? OriginalRequestObject { get; }

    /*
     * RFC 9101 §6.3 (which updates OpenID Connect Core §6.1's merge): when a request object is used, the
     * Authorization Server MUST only use the parameters in the Request Object, even if the same parameter is provided
     * in the query parameter. A parameter sent only outside the request object is therefore ignored rather than merged
     * in, so it can never bypass the request object's signature (FAPI 1.0 Advanced §5.2.2-10). That includes
     * 'response_type' and 'scope', which OpenID Connect Core also has a client send outside: the request object must
     * carry them, an outer 'response_type' must match it, and an outer 'scope' is ignored (OpenID Connect Core §6.1 lets
     * the object's value supersede it). Only 'client_id', which the OAuth syntax carries to identify the client, falls
     * back to the outer value; the request object must carry a matching one (both validated with the request object).
     */

    // The values every parameter except client_id is read from.
    private IBaseAuthorizationRequestValues Values =>
        (IBaseAuthorizationRequestValues?)OriginalRequestObject ?? OriginalRequestMessage;

    /// <inheritdoc />
    public IReadOnlyList<string> AcrValues => Values.AcrValues ?? [];

    /// <inheritdoc />
    public IRequestClaims? Claims => Values.Claims;

    /// <inheritdoc />
    public IReadOnlyList<string> ClaimsLocales => Values.ClaimsLocales ?? [];

    /// <inheritdoc />
    public string ClientId =>
        OriginalRequestObject?.ClientId
        ?? OriginalRequestMessage.ClientId
        ?? throw OpenIdEnvironment
            .ErrorFactory.MissingParameter(OpenIdProtocol.Parameters.ClientId)
            .AsException();

    /// <inheritdoc />
    public string? CodeChallenge => Values.CodeChallenge;

    /// <inheritdoc />
    public string? CodeChallengeMethod => Values.CodeChallengeMethod;

    /// <inheritdoc />
    public string? CodeVerifier => Values.CodeVerifier;

    /// <inheritdoc />
    public string? DPoPJwkThumbprint => Values.DPoPJwkThumbprint;

    /// <inheritdoc />
    public string DisplayType => Values.DisplayType ?? OpenIdConstants.DisplayTypes.Page;

    /// <inheritdoc />
    public string? IdTokenHint => Values.IdTokenHint;

    /// <inheritdoc />
    public string? LoginHint => Values.LoginHint;

    /// <inheritdoc />
    public TimeSpan? MaxAge => Values.MaxAge;

    /// <inheritdoc />
    public string? Nonce => Values.Nonce;

    /// <inheritdoc />
    public IReadOnlyList<string> PromptTypes => Values.PromptTypes ?? [];

    /// <inheritdoc />
    public string RedirectUri =>
        Values.RedirectUri
        ?? throw OpenIdEnvironment
            .ErrorFactory.MissingParameter(OpenIdProtocol.Parameters.RedirectUri)
            .AsException();

    /// <inheritdoc />
    public IReadOnlyList<string> Resources => Values.Resources ?? [];

    /// <inheritdoc />
    public string? ResponseMode => Values.ResponseMode;

    /// <inheritdoc />
    public IReadOnlyList<string> ResponseTypes => Values.ResponseTypes ?? [];

    /// <inheritdoc />
    public IReadOnlyList<string> Scopes => GrantedScopes ?? Values.Scopes ?? [];

    /// <inheritdoc />
    public string? State => Values.State;

    /// <inheritdoc />
    public IReadOnlyList<string> UiLocales => Values.UiLocales ?? [];

    /// <inheritdoc />
    public IAuthorizationRequest Clone() => new AuthorizationRequest(this);
}
