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
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class ValidateAuthorizationRequestHandler : ICommandHandler<ValidateAuthorizationRequestCommand>
{
    private IOpenIdErrorFactory ErrorFactory { get; }

    public ValidateAuthorizationRequestHandler(IOpenIdErrorFactory errorFactory)
    {
        ErrorFactory = errorFactory;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ValidateAuthorizationRequestCommand command, CancellationToken cancellationToken)
    {
        var authorizationRequest = command.AuthorizationRequest;

        ValidateRequestMessage(authorizationRequest.OriginalRequestMessage);

        if (authorizationRequest.OriginalRequestObject != null)
            ValidateRequestObject(authorizationRequest.OriginalRequestMessage, authorizationRequest.OriginalRequestObject);

        ValidateRequest(authorizationRequest);

        return ValueTask.CompletedTask;
    }

    [AssertionMethod]
    private void ValidateRequestMessage(IAuthorizationRequestMessage requestMessage)
    {
        var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
        if (responseType == ResponseTypes.Unspecified)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.ResponseType).AsException();

        if (responseType.HasFlag(ResponseTypes.None) && responseType != ResponseTypes.None)
            throw ErrorFactory.InvalidRequest("The 'none' response_type must not be combined with other values.").AsException();

        var redirectUri = requestMessage.RedirectUri;
        if (redirectUri is null)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.RedirectUri).AsException();
    }

    [AssertionMethod]
    private void ValidateRequestObject(IAuthorizationRequestMessage requestMessage, IAuthorizationRequestObject requestObject)
    {
        var errorCode = requestObject.RequestObjectSource == RequestObjectSource.Remote ?
            OpenIdConstants.ErrorCodes.InvalidRequestUri :
            OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        /*
         * request and request_uri parameters MUST NOT be included in Request Objects.
         */

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.Request))
            throw ErrorFactory.InvalidRequest("The JWT request object must not contain the 'request' parameter.", errorCode).AsException();

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.RequestUri))
            throw ErrorFactory.InvalidRequest("The JWT request object must not contain the 'request_uri' parameter.", errorCode).AsException();

        /*
         * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
         * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
         * match those in the Request Object, if present.
         */

        if (requestObject.ResponseType != null && requestObject.ResponseType != requestMessage.ResponseType)
            throw ErrorFactory.InvalidRequest("The 'response_type' parameter in the JWT request object must match the same value from the request message.", errorCode).AsException();

        /*
         * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
         */

        if (string.IsNullOrEmpty(requestObject.ClientId))
            throw ErrorFactory.MissingParameter("The 'client_id' parameter in the JWT request object is missing.", errorCode).AsException();

        if (!string.Equals(requestObject.ClientId, requestMessage.ClientId, StringComparison.Ordinal))
            throw ErrorFactory.InvalidRequest("The 'client_id' parameter in the JWT request object must match the same value from the request message.", errorCode).AsException();
    }

    [AssertionMethod]
    private void ValidateRequest(IAuthorizationRequestUnion request)
    {
        var hasOpenIdScope = request.Scopes.Contains(OpenIdConstants.ScopeTypes.OpenId);
        var isImplicit = request.GrantType == GrantType.Implicit;
        var isHybrid = request.GrantType == GrantType.Hybrid;

        var hasCodeChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var codeChallengeMethodIsPlain = request.CodeChallengeMethod == CodeChallengeMethod.Plain;

        var redirectUris = request.Client.RedirectUris;
        if (!redirectUris.Contains(request.RedirectUri) && !(request.Client.AllowLoopback && request.RedirectUri.IsLoopback))
            throw ErrorFactory.InvalidRequest($"The specified '{OpenIdConstants.Parameters.RedirectUri}' is not valid for this client application.").AsException();

        // TODO: error messages must now use redirection

        if (request.Client.IsDisabled)
            throw ErrorFactory.UnauthorizedClient("The client is disabled.").AsException();

        if (request.Scopes.Count == 0)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.Scope).AsException();

        if (request.ResponseType == ResponseTypes.Unspecified)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.ResponseType).AsException();

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(request.Nonce))
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.Nonce).AsException();

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && !hasOpenIdScope)
            throw ErrorFactory.InvalidRequest("The openid scope is required when requesting id tokens.").AsException();

        if (request.ResponseMode == ResponseMode.Query && request.GrantType != GrantType.AuthorizationCode)
            throw ErrorFactory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.").AsException();

        if (request.PromptType.HasFlag(PromptTypes.None) && request.PromptType != PromptTypes.None)
            throw ErrorFactory.InvalidRequest("The 'none' prompt must not be combined with other values.").AsException();

        if (request.PromptType.HasFlag(PromptTypes.CreateAccount) && request.PromptType != PromptTypes.CreateAccount)
            throw ErrorFactory.InvalidRequest("The 'create' prompt must not be combined with other values.").AsException();

        if (hasOpenIdScope && string.IsNullOrEmpty(request.Nonce) && (isImplicit || isHybrid))
            throw ErrorFactory.InvalidRequest("The nonce parameter is required when using the implicit or hybrid flows for openid requests.").AsException();

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        if (request.ResponseType.HasFlag(ResponseTypes.Token) && !request.Client.AllowUnsafeTokenResponse)
            throw ErrorFactory.UnauthorizedClient("The client configuration prohibits the use of unsafe token responses.").AsException();

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (!hasCodeChallenge && request.Client.RequirePkce)
            throw ErrorFactory.UnauthorizedClient("The client configuration requires the use of PKCE parameters.").AsException();

        if (hasCodeChallenge && codeChallengeMethodIsPlain && !request.Client.AllowPlainCodeChallengeMethod)
            throw ErrorFactory.UnauthorizedClient("The client configuration prohibits the plain PKCE method.").AsException();

        // TODO: check allowed grant types from client configuration

        // TODO: check allowed scopes from client configuration

        // TODO: check allowed IdP from client configuration

        // TODO: add support for Resource Indicators
        // https://datatracker.ietf.org/doc/html/rfc8707

        // TODO: check session cookie
        // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L801
    }
}
