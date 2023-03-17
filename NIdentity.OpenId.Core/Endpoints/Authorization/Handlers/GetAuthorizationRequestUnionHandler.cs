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

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Mediator;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class GetAuthorizationRequestUnionHandler : IRequestResponseHandler<GetAuthorizationRequestUnionRequest, IAuthorizationRequestUnion>
{
    private ILogger<GetAuthorizationRequestUnionHandler> Logger { get; }
    private AuthorizationOptions Options { get; }
    private IOpenIdErrorFactory ErrorFactory { get; }
    private IHttpClientFactory HttpClientFactory { get; }
    private IClientStore ClientStore { get; }
    private ISecretService SecretService { get; }
    private IJwtDecoder JwtDecoder { get; }

    public GetAuthorizationRequestUnionHandler(
        ILogger<GetAuthorizationRequestUnionHandler> logger,
        IOptions<AuthorizationOptions> optionsAccessor,
        IOpenIdErrorFactory errorFactory,
        IHttpClientFactory httpClientFactory,
        IClientStore clientStore,
        ISecretService secretService,
        IJwtDecoder jwtDecoder)
    {
        Logger = logger;
        Options = optionsAccessor.Value;
        ErrorFactory = errorFactory;
        HttpClientFactory = httpClientFactory;
        ClientStore = clientStore;
        SecretService = secretService;
        JwtDecoder = jwtDecoder;
    }

    /// <inheritdoc />
    public async ValueTask<IAuthorizationRequestUnion> HandleAsync(
        GetAuthorizationRequestUnionRequest request,
        CancellationToken cancellationToken)
    {
        var requestMessage = AuthorizationRequestMessage.Load(request.AuthorizationRequestStringValues);

        requestMessage.AuthorizationSource = request.AuthorizationRequestStringValues.AuthorizationSource;

        var client = await GetClientAsync(requestMessage, cancellationToken);

        var requestObject = await LoadRequestObjectAsync(requestMessage, client, cancellationToken);

        return new AuthorizationRequestUnion(requestMessage, requestObject, client);
    }

    private async ValueTask<Client> GetClientAsync(
        IAuthorizationRequestMessage requestMessage,
        CancellationToken cancellationToken)
    {
        var clientId = requestMessage.ClientId;
        if (string.IsNullOrEmpty(clientId))
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.ClientId).AsException();

        var client = await ClientStore.TryGetByClientIdAsync(clientId, cancellationToken);
        if (client == null)
            throw ErrorFactory.InvalidRequest("The 'client_id' parameter is invalid.").AsException();

        return client;
    }

    private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(
        IAuthorizationRequestMessage requestMessage,
        Client client,
        CancellationToken cancellationToken)
    {
        var requestJwt = requestMessage.RequestJwt;
        var requestUri = requestMessage.RequestUri;

        RequestObjectSource requestObjectSource;
        string errorCode;

        if (requestUri is not null)
        {
            requestObjectSource = RequestObjectSource.Remote;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

            if (!string.IsNullOrEmpty(requestJwt))
                throw ErrorFactory
                    .InvalidRequest("Both the request and request_uri parameters cannot be present at the same time.", errorCode)
                    .AsException();

            if (!Options.RequestObject.RequestUriEnabled)
                throw ErrorFactory.RequestUriNotSupported().AsException();

            var requestUriMaxLength = Options.RequestObject.RequestUriMaxLength;
            if (requestUri.OriginalString.Length > requestUriMaxLength)
                throw ErrorFactory
                    .InvalidRequest($"The request_uri parameter must not exceed {requestUriMaxLength} characters.", errorCode)
                    .AsException();

            requestJwt = await FetchRequestUriAsync(client, requestUri, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(requestJwt))
        {
            requestObjectSource = RequestObjectSource.Inline;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;

            if (!Options.RequestObject.RequestJwtEnabled)
                throw ErrorFactory.RequestJwtNotSupported().AsException();
        }
        else if (client.RequireRequestObject)
        {
            throw ErrorFactory
                .InvalidRequest("Client configuration requires the use of request or request_uri parameters.")
                .AsException();
        }
        else
        {
            return null;
        }

        string json;
        try
        {
            using var securityKeys = SecretService.LoadSecurityKeys(client.Secrets);

            var issuer = client.ClientId;
            var audience = Options.RequestObject.Audience;

            json = JwtDecoder.DecodeJwt(requestJwt, issuer, audience, securityKeys);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to decode JWT");
            throw ErrorFactory.FailedToDecodeJwt(errorCode).AsException(exception);
        }

        try
        {
            // this will deserialize the object using: OpenIdMessageJsonConverterFactory => OpenIdMessageJsonConverter => OpenIdMessage.Load
            var requestObject = JsonSerializer.Deserialize<AuthorizationRequestObject>(json, requestMessage.OpenIdContext.JsonSerializerOptions);
            if (requestObject == null)
                throw new JsonException("TODO");

            requestObject.RequestObjectSource = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to deserialize JSON");
            throw ErrorFactory.FailedToDeserializeJson(errorCode).AsException(exception);
        }
    }

    private async ValueTask<string> FetchRequestUriAsync(
        Client client,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        request.Options.Set(new HttpRequestOptionsKey<Client>("Client"), client);

        using var httpClient = HttpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw ErrorFactory
                    .InvalidRequestUri($"The http status code of the response must be 200 OK. Received {(int)response.StatusCode} {response.StatusCode}.")
                    .AsException();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var expectedContentType = Options.RequestObject.ExpectedContentType;
            if (Options.RequestObject.StrictContentType && contentType != expectedContentType)
                throw ErrorFactory
                    .InvalidRequestUri($"The content type of the response must be '{expectedContentType}'. Received '{contentType}'.")
                    .AsException();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to fetch the request URI");
            throw ErrorFactory
                .InvalidRequestUri("Failed to fetch the request URI")
                .AsException(exception);
        }
    }
}
