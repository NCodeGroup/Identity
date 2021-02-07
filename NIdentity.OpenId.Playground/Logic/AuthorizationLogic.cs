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

        public AuthorizationLogic(ILogger<AuthorizationLogic> logger, IOptions<AuthorizationOptions> optionsAccessor, IHttpClientFactory httpClientFactory, IOpenIdMessageFactory openIdMessageFactory, IClientStore clientStore, ISecretService secretService, IJwtDecoder jwtDecoder)
        {
            _logger = logger;
            _options = optionsAccessor.Value;
            _httpClientFactory = httpClientFactory;
            _openIdMessageFactory = openIdMessageFactory;
            _clientStore = clientStore;
            _secretService = secretService;
            _jwtDecoder = jwtDecoder;
        }

        public async ValueTask ValidateRequestMessageOnlyAsync(IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var scopes = requestMessage.Scopes ?? Array.Empty<string>();
            if (scopes.Count == 0)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Scope);

            var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
            if (responseType == ResponseTypes.Unspecified)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

            if (string.IsNullOrEmpty(requestMessage.ClientId))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ClientId);

            if (string.IsNullOrEmpty(requestMessage.RedirectUri))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);

            if (!Uri.TryCreate(requestMessage.RedirectUri, UriKind.Absolute, out _))
                throw OpenIdException.Factory.InvalidParameterValue(OpenIdConstants.Parameters.RedirectUri);

            var grantType = DetermineGrantType(responseType);
            if (grantType == GrantType.Unspecified)
                throw OpenIdException.Factory.InvalidParameterValue(OpenIdConstants.Parameters.ResponseType);
            if (responseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(requestMessage.Nonce))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Nonce);

            var responseMode = requestMessage.ResponseMode ?? DetermineDefaultResponseNode(grantType);
            if (responseMode == ResponseMode.Query && grantType != GrantType.AuthorizationCode)
                throw OpenIdException.Factory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.");

            var promptType = requestMessage.PromptType ?? PromptTypes.Unspecified;
            if (promptType.HasFlag(PromptTypes.None) && promptType != PromptTypes.None)
                throw OpenIdException.Factory.InvalidRequest("The 'none' prompt must not be combined with other values.");

            // other defaults
            var codeChallengeMethod = requestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;
            var displayType = requestMessage.DisplayType ?? DisplayType.Unspecified;

            var client = await _clientStore.GetByClientIdAsync(requestMessage.ClientId, cancellationToken);
            if (client == null)
                throw OpenIdException.Factory.InvalidRequest("TODO");
            if (client.IsDisabled)
                throw OpenIdException.Factory.InvalidRequest("TODO");
        }

        public async ValueTask ValidateRequestMessageWithObjectAsync(IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var scopes = requestMessage.Scopes ?? Array.Empty<string>();
            if (scopes.Count == 0)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Scope);

            var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
            if (responseType == ResponseTypes.Unspecified)
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

            if (string.IsNullOrEmpty(requestMessage.ClientId))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ClientId);

            if (string.IsNullOrEmpty(requestMessage.RedirectUri))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);

            if (!Uri.TryCreate(requestMessage.RedirectUri, UriKind.Absolute, out _))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);

            var grantType = DetermineGrantType(responseType);
            if (grantType == GrantType.Unspecified)
                throw OpenIdException.Factory.InvalidParameterValue(OpenIdConstants.Parameters.ResponseType);
            if (responseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(requestMessage.Nonce))
                throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Nonce);

            var responseMode = requestMessage.ResponseMode ?? DetermineDefaultResponseNode(grantType);
            if (responseMode == ResponseMode.Query && grantType != GrantType.AuthorizationCode)
                throw OpenIdException.Factory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.");

            var promptType = requestMessage.PromptType ?? PromptTypes.Unspecified;
            if (promptType.HasFlag(PromptTypes.None) && promptType != PromptTypes.None)
                throw OpenIdException.Factory.InvalidRequest("The 'none' prompt must not be combined with other values.");

            // other defaults
            var codeChallengeMethod = requestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;
            var displayType = requestMessage.DisplayType ?? DisplayType.Unspecified;

            var client = await _clientStore.GetByClientIdAsync(requestMessage.ClientId, cancellationToken);
            if (client == null)
                throw OpenIdException.Factory.InvalidRequest("TODO");
            if (client.IsDisabled)
                throw OpenIdException.Factory.InvalidRequest("TODO");

            var requestObject = await LoadRequestObjectAsync(client, requestMessage, cancellationToken);
            if (requestObject != null)
            {
                ValidateRequestObject(requestMessage, requestObject);
            }
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

            if (requestObject.ResponseType == null || requestObject.ResponseType != requestMessage.ResponseType)
                throw OpenIdException.Factory.InvalidRequest("The 'response_type' parameter in the JWT request object must match the same value from the request message.", errorCode);

            var grantType = DetermineGrantType(requestObject.ResponseType.Value);

            /*
             * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
             */

            if (string.IsNullOrEmpty(requestObject.ClientId))
                throw OpenIdException.Factory.MissingParameter("The 'client_id' parameter in the JWT request object is missing.", errorCode);

            if (!string.Equals(requestObject.ClientId, requestMessage.ClientId, StringComparison.Ordinal))
                throw OpenIdException.Factory.InvalidRequest("The 'client_id' parameter in the JWT request object must match the same value from the request message.", errorCode);

            /*
             * The Authorization Server MUST assemble the set of Authorization Request parameters to be used from the Request Object value and the
             * OAuth 2.0 Authorization Request parameters (minus the request or request_uri parameters). If the same parameter exists both in the
             * Request Object and the OAuth Authorization Request parameters, the parameter in the Request Object is used. Using the assembled set
             * of Authorization Request parameters, the Authorization Server then validates the request the normal manner for the flow being used,
             * as specified in Sections 3.1.2.2, 3.2.2.2, or 3.3.2.2.
             */

            var acrValues = requestObject.AcrValues ?? requestMessage.AcrValues ?? Array.Empty<string>();
            var claims = requestObject.Claims ?? requestMessage.Claims;
            var claimsLocales = requestObject.ClaimsLocales ?? requestMessage.ClaimsLocales ?? Array.Empty<string>();
            var clientId = requestObject.ClientId;
            var codeChallenge = requestObject.CodeChallenge ?? requestMessage.CodeChallenge; // TODO
            var codeChallengeMethod = requestObject.CodeChallengeMethod ?? requestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;
            var codeVerifier = requestObject.CodeVerifier ?? requestMessage.CodeVerifier; // TODO
            var displayType = requestObject.DisplayType ?? requestMessage.DisplayType ?? DisplayType.Unspecified;
            var idTokenHint = requestObject.IdTokenHint ?? requestMessage.IdTokenHint;
            var loginHint = requestObject.LoginHint ?? requestMessage.LoginHint;
            var maxAge = requestObject.MaxAge ?? requestMessage.MaxAge;
            var nonce = requestObject.Nonce ?? requestMessage.Nonce;
            var promptType = requestObject.PromptType ?? requestMessage.PromptType ?? PromptTypes.Unspecified;
            var redirectUri = requestObject.RedirectUri ?? requestMessage.RedirectUri;
            var responseMode = requestObject.ResponseMode ?? requestMessage.ResponseMode ?? DetermineDefaultResponseNode(grantType);
            var responseType = requestObject.ResponseType ?? requestMessage.ResponseType; // TODO
            var scopes = requestObject.Scopes ?? requestMessage.Scopes ?? Array.Empty<string>();
            var state = requestObject.State ?? requestMessage.State;
            var uiLocales = requestObject.UiLocales ?? requestMessage.UiLocales ?? Array.Empty<string>();
        }

        public async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(Client client, IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var requestJwt = requestMessage.Request;
            var requestUri = requestMessage.RequestUri;

            RequestObjectSource requestObjectSource;
            string errorCode;

            if (!string.IsNullOrEmpty(requestUri))
            {
                requestObjectSource = RequestObjectSource.RequestUri;
                errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

                if (!string.IsNullOrEmpty(requestJwt))
                    throw OpenIdException.Factory.InvalidRequest("Both the request and request_uri parameters cannot be present at the same time.", errorCode);

                if (!_options.RequestObject.RequestUriEnabled)
                    throw OpenIdException.Factory.RequestUriNotSupported();

                var requestUriMaxLength = _options.RequestObject.RequestUriMaxLength;
                if (requestUri.Length > requestUriMaxLength)
                    throw OpenIdException.Factory.InvalidRequest($"The request_uri parameter must not exceed {requestUriMaxLength} characters.", errorCode);

                requestJwt = await FetchRequestUriAsync(client, requestUri, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(requestJwt))
            {
                requestObjectSource = RequestObjectSource.Request;
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

            //var converter = new OpenIdMessageJsonConverter<AuthorizationRequestObject>();
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            try
            {
                var requestObject = JsonSerializer.Deserialize<AuthorizationRequestObject>(json, options);
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

        private async ValueTask<string> FetchRequestUriAsync(Client client, string requestUri, CancellationToken cancellationToken)
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

        private static GrantType DetermineGrantType(ResponseTypes responseType)
        {
            return responseType switch
            {
                ResponseTypes.Unspecified => GrantType.Unspecified,
                ResponseTypes.Code => GrantType.AuthorizationCode,
                _ => responseType.HasFlag(ResponseTypes.Code) ? GrantType.Hybrid : GrantType.Implicit
            };
        }

        private static ResponseMode DetermineDefaultResponseNode(GrantType grantType)
        {
            return grantType == GrantType.AuthorizationCode ? ResponseMode.Query : ResponseMode.Fragment;
        }

        public IAuthorizationRequestMessage LoadRequestMessage(HttpContext httpContext)
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

            var context = _openIdMessageFactory.CreateContext();
            var message = _openIdMessageFactory.Create<IAuthorizationRequestMessage>(context);

            message.Load(source);

            return message;
        }
    }
}
