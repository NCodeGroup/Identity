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
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="LoadBackchannelAuthenticationRequestCommand"/> handler. When the
/// client sends a signed <c>request</c> JWT (OpenID Connect CIBA §7.1.1), it validates the JWT against the client's keys
/// and uses its claims as the authoritative request; otherwise the form-based request is returned as-is. The JWT must
/// carry an asymmetric signature the client's effective settings permit, and is never encrypted: CIBA defines no
/// encrypted authentication request (ADR-0155).
/// </summary>
internal class DefaultLoadBackchannelAuthenticationRequestHandler(
    ILogger<DefaultLoadBackchannelAuthenticationRequestHandler> logger,
    IJwtValidator jwtValidator,
    IClientKeyResolver clientKeyResolver
)
    : ICommandResponseHandler<
        LoadBackchannelAuthenticationRequestCommand,
        IBackchannelAuthenticationRequest
    >
{
    private ILogger<DefaultLoadBackchannelAuthenticationRequestHandler> Logger { get; } = logger;
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private IClientKeyResolver ClientKeyResolver { get; } = clientKeyResolver;

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
        try
        {
            var validationKeys = await ClientKeyResolver.ResolveSecretKeysAsync(
                openIdClient,
                cancellationToken
            );

            // No decryption keys are given, so an encrypted request is rejected; the signature algorithm is one the
            // client's effective settings permit, checked before the signature is verified (ADR-0154).
            var settings = openIdClient.Settings;
            var parameters = new JwtValidationParameters
            {
                // Honor the client's effective clock_skew setting, as the authorization endpoint's request objects do;
                // without it the JWT validator would fall back to its 5-minute default.
                ClockSkew = settings.GetValue(OpenIdSettingKeys.ClockSkew),
                ValidSignatureAlgorithms = settings.GetPermittedAlgorithms(
                    OpenIdSettingKeys.BackchannelAuthenticationRequestSigningAlgValuesSupported
                ),
            }
                .UseValidationKeys(validationKeys)
                .ValidateIssuer(openIdClient.ClientId)
                .ValidateAudience(openIdContext.Tenant.Issuer)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime(options =>
                {
                    // CIBA Core 1.0 §7.1.1: a signed authentication request carries 'iat', 'nbf', and 'exp', and is
                    // valid for a bounded time like any request object (ADR-0187).
                    options.RequireExpirationTime = true;
                    options.RequireIssuedAt = true;
                    options.RequireNotBefore = true;
                    options.MaxLifetime = settings.GetValue(
                        OpenIdSettingKeys.RequestObjectMaxLifetime
                    );
                })
                .RequireAssertion(
                    static (context, _) =>
                        ValueTask.FromResult(
                            context.DecodedJwt.Payload.TryGetProperty(
                                JoseClaimNames.Payload.Jti,
                                out var jti
                            )
                                && jti.ValueKind == JsonValueKind.String
                                && !string.IsNullOrEmpty(jti.GetString())
                        )
                );

            var result = await JwtValidator.ValidateAsync(
                requestJwt,
                parameters,
                cancellationToken
            );

            if (!result.IsValid)
            {
                ExceptionDispatchInfo.Throw(result.Exception);
            }

            jwtPayload = result.DecodedJwt.Payload;
        }
        catch (Exception exception)
        {
            Logger.FailedToDecodeJwt(exception);
            throw errorFactory.FailedToDecodeJwt(errorCode).WithException(exception).AsException();
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
