#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using NIdentity.OpenId.Handlers;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Requests.Authorization;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class ValidateAuthorizationRequestHandler : IRequestHandler<ValidateAuthorizationRequest>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ValidateAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var authorizationRequest = request.AuthorizationRequest;

        ValidateRequestMessage(authorizationRequest.OriginalRequestMessage);

        if (authorizationRequest.OriginalRequestObject != null)
            ValidateRequestObject(authorizationRequest.OriginalRequestMessage, authorizationRequest.OriginalRequestObject);

        ValidateRequest(authorizationRequest);

        return ValueTask.CompletedTask;
    }

    [AssertionMethod]
    private static void ValidateRequestMessage(IAuthorizationRequestMessage requestMessage)
    {
        var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
        if (responseType == ResponseTypes.Unspecified)
            throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

        if (responseType.HasFlag(ResponseTypes.None) && responseType != ResponseTypes.None)
            throw OpenIdException.Factory.InvalidRequest("The 'none' response_type must not be combined with other values.");

        var redirectUri = requestMessage.RedirectUri;
        if (redirectUri is null)
            throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);
    }

    [AssertionMethod]
    private static void ValidateRequestObject(IAuthorizationRequestMessage requestMessage, IAuthorizationRequestObject requestObject)
    {
        var errorCode = requestObject.Source == RequestObjectSource.RequestUri ?
            OpenIdConstants.ErrorCodes.InvalidRequestUri :
            OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        /*
         * request and request_uri parameters MUST NOT be included in Request Objects.
         */

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.Request))
            throw OpenIdException.Factory.InvalidRequest("The JWT request object must not contain the 'request' parameter.", errorCode);

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.RequestUri))
            throw OpenIdException.Factory.InvalidRequest("The JWT request object must not contain the 'request_uri' parameter.", errorCode);

        /*
         * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
         * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
         * match those in the Request Object, if present.
         */

        if (requestObject.ResponseType != null && requestObject.ResponseType != requestMessage.ResponseType)
            throw OpenIdException.Factory.InvalidRequest("The 'response_type' parameter in the JWT request object must match the same value from the request message.", errorCode);

        /*
         * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
         */

        if (string.IsNullOrEmpty(requestObject.ClientId))
            throw OpenIdException.Factory.MissingParameter("The 'client_id' parameter in the JWT request object is missing.", errorCode);

        if (!string.Equals(requestObject.ClientId, requestMessage.ClientId, StringComparison.Ordinal))
            throw OpenIdException.Factory.InvalidRequest("The 'client_id' parameter in the JWT request object must match the same value from the request message.", errorCode);
    }

    [AssertionMethod]
    private static void ValidateRequest(IAuthorizationRequest request)
    {
        var hasOpenIdScope = request.Scopes.Contains(OpenIdConstants.ScopeTypes.OpenId);
        var isImplicit = request.GrantType == GrantType.Implicit;
        var isHybrid = request.GrantType == GrantType.Hybrid;

        var hasCodeChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var codeChallengeMethodIsPlain = request.CodeChallengeMethod == CodeChallengeMethod.Plain;

        var redirectUris = request.Client.RedirectUris;

        if (request.Scopes.Count == 0)
            throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Scope);

        if (request.ResponseType == ResponseTypes.Unspecified)
            throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(request.Nonce))
            throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Nonce);

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && !hasOpenIdScope)
            throw OpenIdException.Factory.InvalidRequest("The openid scope is required when requesting id tokens.");

        if (request.ResponseMode == ResponseMode.Query && request.GrantType != GrantType.AuthorizationCode)
            throw OpenIdException.Factory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.");

        if (request.PromptType.HasFlag(PromptTypes.None) && request.PromptType != PromptTypes.None)
            throw OpenIdException.Factory.InvalidRequest("The 'none' prompt must not be combined with other values.");

        if (hasOpenIdScope && string.IsNullOrEmpty(request.Nonce) && (isImplicit || isHybrid))
            throw OpenIdException.Factory.InvalidRequest("The nonce parameter is required when using the implicit or hybrid flows for openid requests.");

        if (request.Client.IsDisabled)
            throw OpenIdException.Factory.UnauthorizedClient("The client is disabled.");

        if (!redirectUris.Contains(request.RedirectUri) && !(request.Client.AllowLoopback && request.RedirectUri.IsLoopback))
            throw OpenIdException.Factory.InvalidRequest($"The specified '{OpenIdConstants.Parameters.RedirectUri}' is not valid for this client application.");

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        if (request.ResponseType.HasFlag(ResponseTypes.Token) && !request.Client.AllowUnsafeTokenResponse)
            throw OpenIdException.Factory.UnauthorizedClient("The client configuration prohibits the use of unsafe token responses.");

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (!hasCodeChallenge && request.Client.RequirePkce)
            throw OpenIdException.Factory.UnauthorizedClient("The client configuration requires the use of PKCE parameters.");

        if (hasCodeChallenge && codeChallengeMethodIsPlain && !request.Client.AllowPlainCodeChallengeMethod)
            throw OpenIdException.Factory.UnauthorizedClient("The client configuration prohibits the plain PKCE method.");

        // TODO: should we have an assertion for grant types?

        // TODO: check IdP
        // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L784

        // TODO: check session cookie
        // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L801
    }
}
