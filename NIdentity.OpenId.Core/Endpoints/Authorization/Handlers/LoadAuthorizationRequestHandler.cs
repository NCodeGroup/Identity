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

using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.JsonWebTokens;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class LoadAuthorizationRequestHandler : ICommandResponseHandler<LoadAuthorizationRequestCommand, AuthorizationContext>
{
    private ILogger<LoadAuthorizationRequestHandler> Logger { get; }
    private AuthorizationOptions Options { get; }
    private IOpenIdErrorFactory ErrorFactory { get; }
    private IHttpClientFactory HttpClientFactory { get; }
    private IClientStore ClientStore { get; }
    private ISecretSerializer SecretSerializer { get; }
    private IJsonWebTokenService JsonWebTokenService { get; }

    public LoadAuthorizationRequestHandler(
        ILogger<LoadAuthorizationRequestHandler> logger,
        IOptions<AuthorizationOptions> optionsAccessor,
        IOpenIdErrorFactory errorFactory,
        IHttpClientFactory httpClientFactory,
        IClientStore clientStore,
        ISecretSerializer secretSerializer,
        IJsonWebTokenService jsonWebTokenService)
    {
        Logger = logger;
        Options = optionsAccessor.Value;
        ErrorFactory = errorFactory;
        HttpClientFactory = httpClientFactory;
        ClientStore = clientStore;
        SecretSerializer = secretSerializer;
        JsonWebTokenService = jsonWebTokenService;
    }

    /// <inheritdoc />
    public async ValueTask<AuthorizationContext> HandleAsync(
        LoadAuthorizationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationSource = command.AuthorizationSource;
        var requestMessage = AuthorizationRequestMessage.Load(authorizationSource);

        requestMessage.AuthorizationSourceType = authorizationSource.AuthorizationSourceType;

        var client = await GetClientAsync(requestMessage, cancellationToken);

        var requestObject = await LoadRequestObjectAsync(requestMessage, client, cancellationToken);

        var authorizationRequest = new AuthorizationRequest(requestMessage, requestObject);

        return new DefaultAuthorizationContext(client, authorizationRequest);
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
                    .InvalidRequest("Both the 'request' and 'request_uri' parameters cannot be present at the same time.", errorCode)
                    .AsException();

            if (!Options.RequestObject.RequestUriEnabled)
                throw ErrorFactory.RequestUriNotSupported().AsException();

            var requestUriMaxLength = Options.RequestObject.RequestUriMaxLength;
            if (requestUri.OriginalString.Length > requestUriMaxLength)
                throw ErrorFactory
                    .InvalidRequest($"The 'request_uri' parameter must not exceed {requestUriMaxLength} characters.", errorCode)
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
                .InvalidRequest("Client configuration requires the use of 'request' or 'request_uri' parameters.")
                .AsException();
        }
        else
        {
            return null;
        }

        JsonElement jwtPayload;
        try
        {
            // TODO: configure how expired secrets are handled
            var secrets = client.Secrets.Where(secret => secret.ExpiresWhen < DateTimeOffset.UtcNow);
            using var secretKeys = SecretSerializer.DeserializeSecrets(secrets);

            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(secretKeys)
                .ValidateIssuer(client.ClientId)
                .ValidateAudience(Options.RequestObject.Audience)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

            var result = await JsonWebTokenService.ValidateJwtAsync(
                requestJwt,
                parameters,
                cancellationToken);

            if (!result.IsValid)
            {
                ExceptionDispatchInfo.Throw(result.Exception);
            }

            jwtPayload = result.DecodedJwt.Payload;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to decode JWT");
            throw ErrorFactory.FailedToDecodeJwt(errorCode).WithException(exception).AsException();
        }

        try
        {
            // this will deserialize the object using: OpenIdMessageJsonConverterFactory => OpenIdMessageJsonConverter => OpenIdMessage.Load
            var requestObject = jwtPayload.Deserialize<AuthorizationRequestObject>(requestMessage.OpenIdMessageContext.JsonSerializerOptions);
            if (requestObject == null)
                throw new JsonException("TODO");

            requestObject.RequestObjectSource = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to deserialize JSON");
            throw ErrorFactory.FailedToDeserializeJson(errorCode).WithException(exception).AsException();
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
                .WithException(exception)
                .AsException();
        }
    }
}
