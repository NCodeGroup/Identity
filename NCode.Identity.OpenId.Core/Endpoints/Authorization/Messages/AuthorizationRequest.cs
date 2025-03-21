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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationRequest"/> abstraction.
/// </summary>
[PublicAPI]
public class AuthorizationRequest : IAuthorizationRequest
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
        IAuthorizationRequestObject? requestObject)
    {
        IsContinuation = isContinuation;
        OriginalRequestMessage = requestMessage;
        OriginalRequestObject = requestObject;
    }

    /// <summary>
    /// Initializes a new instance of the current class that is a clone of another instance.
    /// </summary>
    /// <param name="other">The other instance to clone.</param>
    protected AuthorizationRequest(AuthorizationRequest other)
    {
        IsContinuation = other.IsContinuation;
        OriginalRequestMessage = other.OriginalRequestMessage.Clone();
        OriginalRequestObject = other.OriginalRequestObject?.Clone();
    }

    private static IReadOnlyList<T> Coalesce<T>(IReadOnlyList<T>? first, IReadOnlyList<T>? second) =>
        first ?? second ?? Array.Empty<T>();

    private static string DetermineGrantType(IReadOnlyCollection<string> responseTypes) =>
        responseTypes.Contains(OpenIdConstants.ResponseTypes.Code) ?
            responseTypes.Count == 1 ?
                OpenIdConstants.GrantTypes.AuthorizationCode :
                OpenIdConstants.GrantTypes.Hybrid :
            OpenIdConstants.GrantTypes.Implicit;

    private static string DetermineDefaultResponseMode(string grantType) =>
        grantType == OpenIdConstants.GrantTypes.AuthorizationCode ?
            OpenIdConstants.ResponseModes.Query :
            OpenIdConstants.ResponseModes.Fragment;

    /// <inheritdoc />
    public string AuthorizationSourceType => AuthorizationSourceTypes.Union;

    /// <inheritdoc />
    public OpenIdEnvironment OpenIdEnvironment => OriginalRequestMessage.OpenIdEnvironment;

    /// <inheritdoc />
    public IParameterCollection Parameters => ParametersOrNull ?? ComposeParameters();

    private IParameterCollection ComposeParameters() =>
        OriginalRequestObject is not null ?
            new CompositeParameterCollection(
                OriginalRequestObject.Parameters,
                OriginalRequestMessage.Parameters
            ) :
            OriginalRequestMessage.Parameters;

    /// <inheritdoc />
    public bool IsContinuation { get; set; }

    /// <inheritdoc />
    public IAuthorizationRequestMessage OriginalRequestMessage { get; }

    /// <inheritdoc />
    public IAuthorizationRequestObject? OriginalRequestObject { get; }

    /*
     * The Authorization Server MUST assemble the set of Authorization Request parameters to be used from the Request Object value and the
     * OAuth 2.0 Authorization Request parameters (minus the request or request_uri parameters). If the same parameter exists both in the
     * Request Object and the OAuth Authorization Request parameters, the parameter in the Request Object is used. Using the assembled set
     * of Authorization Request parameters, the Authorization Server then validates the request the normal manner for the flow being used,
     * as specified in Sections 3.1.2.2, 3.2.2.2, or 3.3.2.2.
     */

    /// <inheritdoc />
    public IReadOnlyList<string> AcrValues => Coalesce(
        OriginalRequestObject?.AcrValues,
        OriginalRequestMessage.AcrValues
    );

    /// <inheritdoc />
    public IRequestClaims? Claims =>
        OriginalRequestObject?.Claims ??
        OriginalRequestMessage.Claims;

    /// <inheritdoc />
    public IReadOnlyList<string> ClaimsLocales => Coalesce(
        OriginalRequestObject?.ClaimsLocales,
        OriginalRequestMessage.ClaimsLocales
    );

    /// <inheritdoc />
    public string ClientId =>
        OriginalRequestObject?.ClientId ??
        OriginalRequestMessage.ClientId ??
        throw OpenIdEnvironment
            .ErrorFactory
            .MissingParameter(OpenIdConstants.Parameters.ClientId)
            .AsException();

    /// <inheritdoc />
    public string? CodeChallenge =>
        OriginalRequestObject?.CodeChallenge ??
        OriginalRequestMessage.CodeChallenge;

    /// <inheritdoc />
    public string CodeChallengeMethod =>
        OriginalRequestObject?.CodeChallengeMethod ??
        OriginalRequestMessage.CodeChallengeMethod ??
        OpenIdConstants.CodeChallengeMethods.Plain;

    /// <inheritdoc />
    public string? CodeVerifier =>
        OriginalRequestObject?.CodeVerifier ??
        OriginalRequestMessage.CodeVerifier;

    /// <inheritdoc />
    public string DisplayType =>
        OriginalRequestObject?.DisplayType ??
        OriginalRequestMessage.DisplayType ??
        OpenIdConstants.DisplayTypes.Page;

    /// <inheritdoc />
    public string GrantType =>
        DetermineGrantType(ResponseTypes);

    /// <inheritdoc />
    public string? IdTokenHint =>
        OriginalRequestObject?.IdTokenHint ??
        OriginalRequestMessage.IdTokenHint;

    /// <inheritdoc />
    public string? LoginHint =>
        OriginalRequestObject?.LoginHint ??
        OriginalRequestMessage.LoginHint;

    /// <inheritdoc />
    public TimeSpan? MaxAge =>
        OriginalRequestObject?.MaxAge ??
        OriginalRequestMessage.MaxAge;

    /// <inheritdoc />
    public string? Nonce =>
        OriginalRequestObject?.Nonce ??
        OriginalRequestMessage.Nonce;

    /// <inheritdoc />
    public IReadOnlyList<string> PromptTypes => Coalesce(
        OriginalRequestObject?.PromptTypes,
        OriginalRequestMessage.PromptTypes
    );

    /// <inheritdoc />
    public Uri RedirectUri =>
        OriginalRequestObject?.RedirectUri ??
        OriginalRequestMessage.RedirectUri ??
        throw OpenIdEnvironment
            .ErrorFactory
            .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
            .AsException();

    /// <inheritdoc />
    public string ResponseMode =>
        OriginalRequestObject?.ResponseMode ??
        OriginalRequestMessage.ResponseMode ??
        DetermineDefaultResponseMode(GrantType);

    /// <inheritdoc />
    public IReadOnlyList<string> ResponseTypes => Coalesce(
        OriginalRequestObject?.ResponseTypes,
        OriginalRequestMessage.ResponseTypes
    );

    /// <inheritdoc />
    public IReadOnlyList<string> Scopes => Coalesce(
        OriginalRequestObject?.Scopes,
        OriginalRequestMessage.Scopes
    );

    /// <inheritdoc />
    public string? State =>
        OriginalRequestObject?.State ??
        OriginalRequestMessage.State;

    /// <inheritdoc />
    public IReadOnlyList<string> UiLocales => Coalesce(
        OriginalRequestObject?.UiLocales,
        OriginalRequestMessage.UiLocales
    );

    /// <inheritdoc />
    public IAuthorizationRequest Clone() => new AuthorizationRequest(this);
}
