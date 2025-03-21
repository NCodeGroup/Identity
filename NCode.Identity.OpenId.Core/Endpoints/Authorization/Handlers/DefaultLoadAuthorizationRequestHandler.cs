﻿#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="LoadAuthorizationRequestCommand"/> message.
/// </summary>
public class DefaultLoadAuthorizationRequestHandler(
    ILogger<DefaultLoadAuthorizationRequestHandler> logger,
    IJsonWebTokenService jsonWebTokenService,
    IHttpClientFactory httpClientFactory
) : ICommandResponseHandler<LoadAuthorizationRequestCommand, IAuthorizationRequest>
{
    private ILogger<DefaultLoadAuthorizationRequestHandler> Logger { get; } = logger;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    /// <inheritdoc />
    public async ValueTask<IAuthorizationRequest> HandleAsync(
        LoadAuthorizationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, openIdRequestValues) = command;

        var openIdEnvironment = openIdContext.Environment;

        // the following will parse string-values into strongly-typed parameters and may throw
        var requestMessage = AuthorizationRequestMessage.Load(openIdEnvironment, openIdRequestValues);
        requestMessage.AuthorizationSourceType = openIdRequestValues.SourceType;

        // TODO: add support for OAuth 2.0 Pushed Authorization Requests (PAR)
        // https://datatracker.ietf.org/doc/html/rfc9126

        var requestObject = await LoadRequestObjectAsync(
            openIdEnvironment,
            openIdClient,
            requestMessage,
            cancellationToken);

        const bool isContinuation = false;
        var authorizationRequest = new AuthorizationRequest(
            isContinuation,
            requestMessage,
            requestObject);

        return authorizationRequest;
    }

    private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdClient openIdClient,
        IAuthorizationRequestMessage requestMessage,
        CancellationToken cancellationToken)
    {
        var errorFactory = openIdEnvironment.ErrorFactory;

        var requestJwt = requestMessage.RequestJwt;
        var requestUri = requestMessage.RequestUri;

        RequestObjectSource requestObjectSource;
        string errorCode;

        if (requestUri is not null)
        {
            if (!openIdClient.Settings.GetValue(SettingKeys.RequestUriParameterSupported))
                throw errorFactory
                    .RequestUriNotSupported()
                    .AsException();

            requestObjectSource = RequestObjectSource.Remote;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

            if (!string.IsNullOrEmpty(requestJwt))
                throw errorFactory
                    .InvalidRequest("Both the 'request' and 'request_uri' parameters cannot be present at the same time.", errorCode)
                    .AsException();

            requestJwt = await FetchRequestUriAsync(
                openIdEnvironment,
                openIdClient,
                requestUri,
                cancellationToken
            );
        }
        else if (!string.IsNullOrEmpty(requestJwt))
        {
            if (!openIdClient.Settings.GetValue(SettingKeys.RequestParameterSupported))
                throw errorFactory
                    .RequestParameterNotSupported()
                    .AsException();

            requestObjectSource = RequestObjectSource.Inline;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;
        }
        else
        {
            return null;
        }

        JsonElement jwtPayload;
        try
        {
            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(openIdClient.SecretKeys)
                .ValidateIssuer(openIdClient.ClientId)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

            var expectedAudience = openIdClient.Settings.GetValue(SettingKeys.RequestObjectExpectedAudience);
            if (!string.IsNullOrEmpty(expectedAudience))
                parameters.ValidateAudience(expectedAudience);

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
            throw errorFactory
                .FailedToDecodeJwt(errorCode)
                .WithException(exception)
                .AsException();
        }

        try
        {
            // this will deserialize the object using OpenIdMessageJsonConverter
            var requestObject = jwtPayload.Deserialize<AuthorizationRequestObject>(openIdEnvironment.JsonSerializerOptions);
            if (requestObject == null)
                throw new InvalidOperationException("JSON deserialization returned null.");

            requestObject.RequestObjectSource = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to deserialize JSON");
            throw errorFactory
                .FailedToDeserializeJson(errorCode)
                .WithException(exception)
                .AsException();
        }
    }

    private async ValueTask<string> FetchRequestUriAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdClient openIdClient,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var errorFactory = openIdEnvironment.ErrorFactory;
        var settings = openIdClient.Settings;

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        request.Options.Set(new HttpRequestOptionsKey<OpenIdClient>("OpenIdClient"), openIdClient);

        using var httpClient = HttpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw errorFactory
                    .InvalidRequestUri($"The http status code of the response must be 200 OK. Received {(int)response.StatusCode} {response.StatusCode}.")
                    .AsException();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var expectedContentType = settings.GetValue(SettingKeys.RequestUriExpectedContentType);
            if (settings.GetValue(SettingKeys.RequestUriRequireStrictContentType) && !string.Equals(contentType, expectedContentType, StringComparison.Ordinal))
                throw errorFactory
                    .InvalidRequestUri($"The content type of the response must be '{expectedContentType}'. Received '{contentType}'.")
                    .AsException();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to fetch the request URI");
            throw errorFactory
                .InvalidRequestUri("Failed to fetch the request URI")
                .WithException(exception)
                .AsException();
        }
    }
}
