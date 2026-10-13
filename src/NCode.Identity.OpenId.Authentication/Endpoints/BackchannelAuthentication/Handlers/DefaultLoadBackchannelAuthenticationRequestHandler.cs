#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="LoadBackchannelAuthenticationRequestCommand"/> handler. When the
/// client sends a signed <c>request</c> JWT (OpenID Connect CIBA §7.1.1), it validates the JWT against the client's
/// registered keys and uses its claims as the authoritative request; otherwise the form-based request is returned as-is.
/// </summary>
internal class DefaultLoadBackchannelAuthenticationRequestHandler(
    ILogger<DefaultLoadBackchannelAuthenticationRequestHandler> logger,
    IJsonWebTokenService jsonWebTokenService
)
    : ICommandResponseHandler<
        LoadBackchannelAuthenticationRequestCommand,
        IBackchannelAuthenticationRequest
    >
{
    private ILogger<DefaultLoadBackchannelAuthenticationRequestHandler> Logger { get; } = logger;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;

    /// <inheritdoc />
    public async ValueTask<IBackchannelAuthenticationRequest> HandleAsync(
        LoadBackchannelAuthenticationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, request) = command;

        var requestJwt = request.RequestJwt;
        if (string.IsNullOrEmpty(requestJwt))
            return request;

        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdEnvironment.ErrorFactory;
        const string errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        JsonElement jwtPayload;
        string? algorithm;
        try
        {
            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(openIdClient.SecretKeys)
                .ValidateIssuer(openIdClient.ClientId)
                .ValidateAudience(openIdContext.Tenant.Issuer)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

            var result = await JsonWebTokenService.ValidateJwtAsync(
                requestJwt,
                parameters,
                cancellationToken
            );

            if (!result.IsValid)
            {
                ExceptionDispatchInfo.Throw(result.Exception);
            }

            jwtPayload = result.DecodedJwt.Payload;
            algorithm = result.DecodedJwt.Header.TryGetProperty(
                JoseClaimNames.Header.Alg,
                out var algElement
            )
                ? algElement.GetString()
                : null;
        }
        catch (Exception exception)
        {
            Logger.FailedToDecodeJwt(exception);
            throw errorFactory.FailedToDecodeJwt(errorCode).WithException(exception).AsException();
        }

        // The request object must be signed with an advertised algorithm; a narrowed set rejects everything else.
        var signingAlgValuesSupported = openIdClient.Settings.GetValue(
            OpenIdSettingKeys.BackchannelAuthenticationRequestSigningAlgValuesSupported
        );
        if (
            signingAlgValuesSupported.Count > 0
            && (string.IsNullOrEmpty(algorithm) || !signingAlgValuesSupported.Contains(algorithm))
        )
        {
            throw errorFactory
                .InvalidRequest(
                    "The backchannel authentication request object is signed with an unsupported algorithm.",
                    errorCode
                )
                .AsException();
        }

        BackchannelAuthenticationRequest requestObject;
        try
        {
            requestObject =
                jwtPayload.Deserialize<BackchannelAuthenticationRequest>(
                    openIdEnvironment.JsonSerializerOptions
                ) ?? throw new InvalidOperationException("JSON deserialization returned null.");
        }
        catch (Exception exception)
        {
            Logger.FailedToDeserializeJson(exception);
            throw errorFactory
                .FailedToDeserializeJson(errorCode)
                .WithException(exception)
                .AsException();
        }

        // A nested request object is not permitted (OpenID Connect CIBA §7.1.1).
        if (!string.IsNullOrEmpty(requestObject.RequestJwt))
        {
            throw errorFactory
                .InvalidRequest(
                    "The 'request' parameter must not be present within the request object.",
                    errorCode
                )
                .AsException();
        }

        return requestObject;
    }
}
