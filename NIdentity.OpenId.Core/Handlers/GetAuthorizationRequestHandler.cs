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
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Handlers;

internal class GetAuthorizationRequestHandler : IRequestResponseHandler<GetAuthorizationRequest, IAuthorizationRequest>
{
    private readonly ILogger<GetAuthorizationRequestHandler> _logger;
    private readonly AuthorizationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClientStore _clientStore;
    private readonly ISecretService _secretService;
    private readonly IJwtDecoder _jwtDecoder;

    public GetAuthorizationRequestHandler(
        ILogger<GetAuthorizationRequestHandler> logger,
        IOptions<AuthorizationOptions> optionsAccessor,
        IHttpClientFactory httpClientFactory,
        IClientStore clientStore,
        ISecretService secretService,
        IJwtDecoder jwtDecoder)
    {
        _logger = logger;
        _options = optionsAccessor.Value;
        _httpClientFactory = httpClientFactory;
        _clientStore = clientStore;
        _secretService = secretService;
        _jwtDecoder = jwtDecoder;
    }

    /// <inheritdoc />
    public async ValueTask<IAuthorizationRequest> HandleAsync(GetAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var httpContext = request.HttpContext;

        var messageContext = new OpenIdMessageContext(_logger);

        var requestMessage = LoadRequestMessage(httpContext, messageContext);
        try
        {
            return await LoadRequestAsync(requestMessage, cancellationToken);
        }
        catch (OpenIdException exception)
        {
            if (!string.IsNullOrEmpty(requestMessage.State))
            {
                exception.WithExtensionData(OpenIdConstants.Parameters.State, requestMessage.State);
            }

            throw;
        }
    }

    private async ValueTask<IAuthorizationRequest> LoadRequestAsync(IAuthorizationRequestMessage requestMessage, CancellationToken cancellationToken)
    {
        var clientId = requestMessage.ClientId;
        if (string.IsNullOrEmpty(clientId))
            throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ClientId);

        var client = await _clientStore.GetByClientIdAsync(clientId, cancellationToken);
        if (client == null)
            throw OpenIdException.Factory.InvalidRequest("TODO: GetByClientIdAsync returned null");

        var requestObject = await LoadRequestObjectAsync(requestMessage, client, cancellationToken);

        return new AuthorizationRequest(requestMessage, requestObject, client);
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
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to decode JWT");
            throw OpenIdException.Factory.FailedToDecodeJwt(errorCode, exception);
        }

        try
        {
            var requestObject = JsonSerializer.Deserialize<AuthorizationRequestObject>(json, requestMessage.Context.JsonSerializerOptions);
            if (requestObject == null)
                throw new JsonException("TODO");

            requestObject.Source = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to deserialize JSON");
            throw OpenIdException.Factory.FailedToDeserializeJson(errorCode, exception);
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
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to fetch the request URI");
            throw OpenIdException.Factory.InvalidRequestUri(exception);
        }
    }

    private static IAuthorizationRequestMessage LoadRequestMessage(HttpContext httpContext, IOpenIdMessageContext messageContext)
    {
        IEnumerable<KeyValuePair<string, StringValues>> parameterStringValues;
        if (HttpMethods.IsGet(httpContext.Request.Method))
        {
            parameterStringValues = httpContext.Request.Query;
        }
        else if (HttpMethods.IsPost(httpContext.Request.Method))
        {
            parameterStringValues = httpContext.Request.Form;
        }
        else
        {
            throw OpenIdException.Factory
                .Create("TODO: errorCode")
                .WithErrorDescription("TODO: errorDescription")
                .WithStatusCode(StatusCodes.Status405MethodNotAllowed);
        }

        return AuthorizationRequestMessage.Load(messageContext, parameterStringValues);
    }
}