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
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
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

        public AuthorizationLogic(ILogger<AuthorizationLogic> logger, IOptions<AuthorizationOptions> optionsAccessor, IHttpClientFactory httpClientFactory, IOpenIdMessageFactory openIdMessageFactory, IClientStore clientStore)
        {
            _logger = logger;
            _options = optionsAccessor.Value;
            _httpClientFactory = httpClientFactory;
            _openIdMessageFactory = openIdMessageFactory;
            _clientStore = clientStore;
        }

        public ValidationResult Handle(HttpContext httpContext)
        {
            if (!TryLoadRequestMessage(httpContext, out var requestMessageResult))
                return requestMessageResult;

            return ValidationResult.SuccessResult;
        }

        public async ValueTask<ValidationResult> ValidateRequestMessageOnlyAsync(IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var scopes = requestMessage.Scopes ?? Array.Empty<string>();
            if (scopes.Count == 0)
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
            if (responseType == ResponseTypes.Unspecified)
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            if (string.IsNullOrEmpty(requestMessage.ClientId))
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            if (string.IsNullOrEmpty(requestMessage.RedirectUri))
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            if (!Uri.TryCreate(requestMessage.RedirectUri, UriKind.Absolute, out var redirectUri))
                return ValidationResult.Factory.InvalidParameterValue("TODO").As<string>();

            var grantType = DetermineGrantType(responseType);
            if (grantType == GrantType.Unspecified)
                return ValidationResult.Factory.InvalidParameterValue("TODO").As<string>();
            if (responseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(requestMessage.Nonce))
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            var responseMode = requestMessage.ResponseMode ?? DetermineDefaultResponseNode(grantType);
            if (responseMode == ResponseMode.Query && grantType != GrantType.AuthorizationCode)
                return ValidationResult.Factory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.").As<string>();

            var promptType = requestMessage.PromptType ?? PromptTypes.Unspecified;
            if (promptType.HasFlag(PromptTypes.None) && promptType != PromptTypes.None)
                return ValidationResult.Factory.InvalidRequest("The 'none' prompt must not be combined with other values.").As<string>();

            // other defaults
            var codeChallengeMethod = requestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;
            var displayType = requestMessage.DisplayType ?? DisplayType.Unspecified;

            var client = await _clientStore.GetByClientIdAsync(requestMessage.ClientId, cancellationToken);
            if (client == null)
                return ValidationResult.Factory.InvalidRequest("TODO").As<string>();
            if (client.IsDisabled)
                return ValidationResult.Factory.InvalidRequest("TODO").As<string>();

            return ValidationResult.SuccessResult;
        }

        public async ValueTask<ValidationResult> ValidateRequestMessageWithObjectAsync(IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var scopes = requestMessage.Scopes ?? Array.Empty<string>();
            if (scopes.Count == 0)
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
            if (responseType == ResponseTypes.Unspecified)
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            if (string.IsNullOrEmpty(requestMessage.ClientId))
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            if (string.IsNullOrEmpty(requestMessage.RedirectUri))
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            if (!Uri.TryCreate(requestMessage.RedirectUri, UriKind.Absolute, out var redirectUri))
                return ValidationResult.Factory.InvalidParameterValue("TODO").As<string>();

            var grantType = DetermineGrantType(responseType);
            if (grantType == GrantType.Unspecified)
                return ValidationResult.Factory.InvalidParameterValue("TODO").As<string>();
            if (responseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(requestMessage.Nonce))
                return ValidationResult.Factory.MissingParameter("TODO").As<string>();

            var responseMode = requestMessage.ResponseMode ?? DetermineDefaultResponseNode(grantType);
            if (responseMode == ResponseMode.Query && grantType != GrantType.AuthorizationCode)
                return ValidationResult.Factory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.").As<string>();

            var promptType = requestMessage.PromptType ?? PromptTypes.Unspecified;
            if (promptType.HasFlag(PromptTypes.None) && promptType != PromptTypes.None)
                return ValidationResult.Factory.InvalidRequest("The 'none' prompt must not be combined with other values.").As<string>();

            // other defaults
            var codeChallengeMethod = requestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;
            var displayType = requestMessage.DisplayType ?? DisplayType.Unspecified;

            var client = await _clientStore.GetByClientIdAsync(requestMessage.ClientId, cancellationToken);
            if (client == null)
                return ValidationResult.Factory.InvalidRequest("TODO").As<string>();
            if (client.IsDisabled)
                return ValidationResult.Factory.InvalidRequest("TODO").As<string>();

            return ValidationResult.SuccessResult;
        }

        private struct GetJwtResult
        {
            public string Jwt { get; set; }

            public string ErrorCodeForErrors { get; set; }
        }

        private async ValueTask<ValidationResult<GetJwtResult?>> GetJwtAsync(Client client, IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var request = requestMessage.Request;
            var requestUri = requestMessage.RequestUri;

            if (!string.IsNullOrEmpty(request))
            {
                if (!string.IsNullOrEmpty(requestUri))
                    return ValidationResult.Factory.InvalidRequest("Both the request and request_uri parameters cannot be present at the same time.").As<GetJwtResult?>();

                return ValidationResult.Factory.Success<GetJwtResult?>(new GetJwtResult
                {
                    Jwt = request,
                    ErrorCodeForErrors = OpenIdConstants.ErrorCodes.InvalidRequestObject
                });
            }

            if (!string.IsNullOrEmpty(requestUri))
            {
                if (!_options.RequestObject.RequestUriEnabled)
                    return ValidationResult.Factory.BadRequest(OpenIdConstants.ErrorCodes.RequestUriNotSupported, "The request_uri parameter is not supported.").As<GetJwtResult?>();

                var requestUriMaxLength = _options.RequestObject.RequestUriMaxLength;
                if (requestUri.Length > requestUriMaxLength)
                    return ValidationResult.Factory.BadRequest(OpenIdConstants.ErrorCodes.InvalidRequestUri, $"The request_uri parameter must not exceed {requestUriMaxLength} characters.").As<GetJwtResult?>();

                var fetchJwtResult = await FetchJwtAsync(client, requestUri, cancellationToken);
                if (fetchJwtResult.HasError)
                    return fetchJwtResult.As<GetJwtResult?>();

                return ValidationResult.Factory.Success<GetJwtResult?>(new GetJwtResult
                {
                    Jwt = fetchJwtResult.Value,
                    ErrorCodeForErrors = OpenIdConstants.ErrorCodes.InvalidRequestUri
                });
            }

            if (client.RequireRequestObject)
                return ValidationResult.Factory.InvalidRequest("Client configuration requires the use of request or request_uri parameters.").As<GetJwtResult?>();

            return ValidationResult.Factory.Success<GetJwtResult?>(null);
        }

        private async ValueTask<ValidationResult<string>> FetchJwtAsync(Client client, string requestUri, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            request.Options.Set(new HttpRequestOptionsKey<Client>("Client"), client);

            using var httpClient = _httpClientFactory.CreateClient();

            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return ValidationResult.Factory.BadRequest(OpenIdConstants.ErrorCodes.InvalidRequestUri, $"The http status code of the response must be OK. Received {response.StatusCode}.").As<string>();

                var contentType = response.Content.Headers.ContentType?.MediaType;
                var expectedContentType = _options.RequestObject.ExpectedContentType;
                if (_options.RequestObject.StrictContentType && contentType != expectedContentType)
                    return ValidationResult.Factory.BadRequest(OpenIdConstants.ErrorCodes.InvalidRequestUri, $"The content type of the response must be \"{expectedContentType}\". Received \"{contentType}\".").As<string>();

                var jwt = await response.Content.ReadAsStringAsync(cancellationToken);
                return ValidationResult.Factory.Success(jwt);
            }
            catch (Exception exception)
            {
                const string errorDescription = "An error occurred while attempting to fetch request_uri.";
                _logger.LogError(exception, errorDescription);
                return ValidationResult.Factory.BadRequest(OpenIdConstants.ErrorCodes.InvalidRequestUri, errorDescription).As<string>();
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

        public bool TryLoadRequestMessage(HttpContext httpContext, out ValidationResult<IAuthorizationRequestMessage> result)
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
                result = ValidationResult.Factory.Error("TODO: error", "TODO: errorDescription", StatusCodes.Status405MethodNotAllowed).As<IAuthorizationRequestMessage>();
                return false;
            }

            var context = _openIdMessageFactory.CreateContext();
            var message = _openIdMessageFactory.Create<IAuthorizationRequestMessage>(context);

            if (!message.TryLoad(source, out var loadResult))
            {
                result = loadResult.As<IAuthorizationRequestMessage>();
                return false;
            }

            result = ValidationResult.Factory.Success(message);
            return true;
        }
    }
}
