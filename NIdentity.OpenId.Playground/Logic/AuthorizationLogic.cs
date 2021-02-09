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

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Playground.Options;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Playground.Logic
{
    internal class AuthorizationLogic
    {
        private readonly ILogger<AuthorizationLogic> _logger;
        private readonly AuthorizationOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOpenIdMessageFactory _openIdMessageFactory;
        private readonly IClientStore _clientStore;
        private readonly ISecretService _secretService;
        private readonly IJwtDecoder _jwtDecoder;

        public AuthorizationLogic(
            ILogger<AuthorizationLogic> logger,
            IOptions<AuthorizationOptions> optionsAccessor,
            IHttpClientFactory httpClientFactory,
            IOpenIdMessageFactory openIdMessageFactory,
            IClientStore clientStore,
            ISecretService secretService,
            IJwtDecoder jwtDecoder)
        {
            _logger = logger;
            _options = optionsAccessor.Value;
            _httpClientFactory = httpClientFactory;
            _openIdMessageFactory = openIdMessageFactory;
            _clientStore = clientStore;
            _secretService = secretService;
            _jwtDecoder = jwtDecoder;
        }

        public async ValueTask HandleAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            var messageContext = _openIdMessageFactory.CreateContext();

            var requestMessage = LoadRequestMessage(httpContext, messageContext);

            var request = await ValidateRequestMessageAsync(requestMessage, cancellationToken);

            ValidateRequest(request);
        }

        private async ValueTask<IAuthorizationRequest> ValidateRequestMessageAsync(IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
            if (responseType == ResponseTypes.Unspecified)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

            var clientId = requestMessage.ClientId;
            if (string.IsNullOrEmpty(clientId))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ClientId);

            var redirectUri = requestMessage.RedirectUri;
            if (redirectUri is null)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);

            var client = await _clientStore.GetByClientIdAsync(clientId, cancellationToken);
            if (client == null)
                throw OpenIdException.Factory.InvalidRequest("TODO");
            if (client.IsDisabled)
                throw OpenIdException.Factory.InvalidRequest("TODO");

            var requestObject = await LoadRequestObjectAsync(requestMessage, client, cancellationToken);
            if (requestObject != null)
            {
                ValidateRequestObject(requestMessage, requestObject);
            }

            return new AuthorizationRequest(requestMessage, requestObject, client);
        }

        [AssertionMethod]
        private static void ValidateRequest(IAuthorizationRequest request)
        {
            if (request.Scopes.Count == 0)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Scope);

            if (request.ResponseType == ResponseTypes.Unspecified)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

            if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(request.Nonce))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Nonce);

            if (request.ResponseMode == ResponseMode.Query && request.GrantType != GrantType.AuthorizationCode)
                throw OpenIdException.Factory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.");

            if (request.PromptType.HasFlag(PromptTypes.None) && request.PromptType != PromptTypes.None)
                throw OpenIdException.Factory.InvalidRequest("The 'none' prompt must not be combined with other values.");
        }

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

        private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(IAuthorizationRequestMessage requestMessage, Client client, CancellationToken cancellationToken)
        {
            var requestJwt = requestMessage.RequestJwt;
            var requestUri = requestMessage.RequestUri;

            RequestObjectSource requestObjectSource;
            string errorCode;

            if (requestUri is not null)
            {
                requestObjectSource = RequestObjectSource.RequestUri;
                errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

                if (!string.IsNullOrEmpty(requestJwt))
                    throw OpenIdException.Factory.InvalidRequest("Both the request and request_uri parameters cannot be present at the same time.", errorCode);

                if (!_options.RequestObject.RequestUriEnabled)
                    throw OpenIdException.Factory.RequestUriNotSupported();

                var requestUriMaxLength = _options.RequestObject.RequestUriMaxLength;
                if (requestUri.OriginalString.Length > requestUriMaxLength)
                    throw OpenIdException.Factory.InvalidRequest($"The request_uri parameter must not exceed {requestUriMaxLength} characters.", errorCode);

                requestJwt = await FetchRequestUriAsync(client, requestUri, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(requestJwt))
            {
                requestObjectSource = RequestObjectSource.RequestJwt;
                errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;

                if (!_options.RequestObject.RequestJwtEnabled)
                    throw OpenIdException.Factory.RequestJwtNotSupported();
            }
            else if (client.RequireRequestObject)
            {
                throw OpenIdException.Factory.InvalidRequest("Client configuration requires the use of request or request_uri parameters.");
            }
            else
            {
                return null;
            }

            string json;
            try
            {
                using var securityKeys = _secretService.LoadSecurityKeys(client.Secrets);

                var issuer = client.ClientId;
                var audience = _options.RequestObject.Audience;

                json = _jwtDecoder.DecodeJwt(requestJwt, issuer, audience, securityKeys);
            }
            catch (Exception innerException)
            {
                var exception = OpenIdException.Factory.FailedToDecodeJwt(errorCode, innerException);
                _logger.LogError(exception, exception.ErrorDescription);
                throw exception;
            }

            try
            {
                var requestObject = JsonSerializer.Deserialize<AuthorizationRequestObject>(json, requestMessage.Context.JsonSerializerOptions);
                if (requestObject == null)
                    throw new JsonException("TODO");

                requestObject.Source = requestObjectSource;

                return requestObject;
            }
            catch (Exception innerException)
            {
                var exception = OpenIdException.Factory.FailedToDeserializeJson(errorCode, innerException);
                _logger.LogError(exception, exception.ErrorDescription);
                throw exception;
            }
        }

        private async ValueTask<string> FetchRequestUriAsync(Client client, Uri requestUri, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            request.Options.Set(new HttpRequestOptionsKey<Client>("Client"), client);

            using var httpClient = _httpClientFactory.CreateClient();

            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw OpenIdException.Factory.InvalidRequestUri($"The http status code of the response must be OK. Received {response.StatusCode}.");

                var contentType = response.Content.Headers.ContentType?.MediaType;
                var expectedContentType = _options.RequestObject.ExpectedContentType;
                if (_options.RequestObject.StrictContentType && contentType != expectedContentType)
                    throw OpenIdException.Factory.InvalidRequestUri($"The content type of the response must be '{expectedContentType}'. Received '{contentType}'.");

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception innerException)
            {
                var exception = OpenIdException.Factory.InvalidRequestUri(innerException);
                _logger.LogError(exception, exception.ErrorDescription);
                throw exception;
            }
        }

        private IAuthorizationRequestMessage LoadRequestMessage(HttpContext httpContext, IOpenIdMessageContext messageContext)
        {
            IEnumerable<KeyValuePair<string, StringValues>> source;
            if (HttpMethods.IsGet(httpContext.Request.Method))
            {
                source = httpContext.Request.Query;
            }
            else if (HttpMethods.IsPost(httpContext.Request.Method))
            {
                source = httpContext.Request.Form;
            }
            else
            {
                throw OpenIdException.Factory
                    .Create("TODO: errorCode")
                    .WithErrorDescription("TODO: errorDescription")
                    .WithStatusCode(StatusCodes.Status405MethodNotAllowed);
            }

            var requestMessage = _openIdMessageFactory.Create<IAuthorizationRequestMessage>(messageContext);

            requestMessage.Load(source);

            return requestMessage;
        }
    }
}
