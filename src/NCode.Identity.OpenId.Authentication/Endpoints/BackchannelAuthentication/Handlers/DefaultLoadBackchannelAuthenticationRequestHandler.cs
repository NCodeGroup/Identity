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
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Json;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="LoadBackchannelAuthenticationRequestCommand"/> handler. When the
/// client sends a signed <c>request</c> JWT (OpenID Connect CIBA §7.1.1), it validates the JWT against the client's
/// registered keys and uses its claims as the authoritative request; otherwise the form-based request is returned as-is.
/// </summary>
internal class DefaultLoadBackchannelAuthenticationRequestHandler(
    ILogger<DefaultLoadBackchannelAuthenticationRequestHandler> logger,
    IJwtValidator jwtValidator
)
    : ICommandResponseHandler<
        LoadBackchannelAuthenticationRequestCommand,
        IBackchannelAuthenticationRequest
    >
{
    private ILogger<DefaultLoadBackchannelAuthenticationRequestHandler> Logger { get; } = logger;
    private IJwtValidator JwtValidator { get; } = jwtValidator;

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
        JsonElement jwtHeader;
        try
        {
            var parameters = new JwtValidationParameters()
                .UseValidationKeys(openIdClient.SecretKeys)
                .ValidateIssuer(openIdClient.ClientId)
                .ValidateAudience(openIdContext.Tenant.Issuer)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

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
            jwtHeader = result.DecodedJwt.Header;
        }
        catch (Exception exception)
        {
            Logger.FailedToDecodeJwt(exception);
            throw errorFactory.FailedToDecodeJwt(errorCode).WithException(exception).AsException();
        }

        ValidateRequestObjectAlgorithms(openIdClient, jwtHeader, errorFactory, errorCode);

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

    // Enforces the request object's algorithms against the advertised sets: the key-management/content-encryption/zip
    // values for an encrypted (JWE) request object, or the signing value for a signed (JWS) one. A narrowed set rejects
    // everything outside it; an unset (empty) set is unrestricted (ADR-0010).
    private static void ValidateRequestObjectAlgorithms(
        OpenIdClient openIdClient,
        JsonElement jwtHeader,
        IOpenIdErrorFactory errorFactory,
        string errorCode
    )
    {
        var settings = openIdClient.Settings;

        var algorithm = jwtHeader.TryGetPropertyValue<string>(
            JoseClaimNames.Header.Alg,
            out var alg
        )
            ? alg
            : null;

        // The presence of an "enc" header identifies an encrypted (JWE) request object.
        if (jwtHeader.TryGetProperty(JoseClaimNames.Header.Enc, out var encElement))
        {
            EnforceAdvertised(
                settings.GetValue(
                    OpenIdSettingKeys.BackchannelAuthenticationRequestEncryptionAlgValuesSupported
                ),
                algorithm,
                "encrypted with an unsupported key-management algorithm",
                errorFactory,
                errorCode
            );

            EnforceAdvertised(
                settings.GetValue(
                    OpenIdSettingKeys.BackchannelAuthenticationRequestEncryptionEncValuesSupported
                ),
                encElement.GetString(),
                "encrypted with an unsupported content-encryption algorithm",
                errorFactory,
                errorCode
            );

            if (
                jwtHeader.TryGetPropertyValue<string>(JoseClaimNames.Header.Zip, out var zip)
                && zip is { Length: > 0 }
            )
            {
                EnforceAdvertised(
                    settings.GetValue(
                        OpenIdSettingKeys.BackchannelAuthenticationRequestEncryptionZipValuesSupported
                    ),
                    zip,
                    "compressed with an unsupported compression algorithm",
                    errorFactory,
                    errorCode
                );
            }

            return;
        }

        // Otherwise it is a signed (JWS) request object.
        EnforceAdvertised(
            settings.GetValue(
                OpenIdSettingKeys.BackchannelAuthenticationRequestSigningAlgValuesSupported
            ),
            algorithm,
            "signed with an unsupported algorithm",
            errorFactory,
            errorCode
        );
    }

    private static void EnforceAdvertised(
        IReadOnlyCollection<string> advertised,
        string? value,
        string reason,
        IOpenIdErrorFactory errorFactory,
        string errorCode
    )
    {
        if (advertised.Count == 0)
            return;

        if (!string.IsNullOrEmpty(value) && advertised.Contains(value))
            return;

        throw errorFactory
            .InvalidRequest(
                $"The backchannel authentication request object is {reason}.",
                errorCode
            )
            .AsException();
    }
}
