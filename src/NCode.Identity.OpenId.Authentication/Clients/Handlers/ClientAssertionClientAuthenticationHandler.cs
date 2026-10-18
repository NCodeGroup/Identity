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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides a common base for the JWT-bearer client-authentication handlers (<c>private_key_jwt</c> and
/// <c>client_secret_jwt</c>) defined by OpenID Connect Core §9 and
/// <see href="https://datatracker.ietf.org/doc/html/rfc7523">RFC 7523</see>. The client presents a signed JWT
/// (<c>client_assertion</c>) whose <c>iss</c> and <c>sub</c> both equal the <c>client_id</c>; the handler verifies the
/// signature against the client's registered keys, enforces the audience/lifetime/single-use rules, and promotes the
/// client to a confidential client on success. The two concrete handlers differ only in the signing-key family they
/// own (asymmetric for <c>private_key_jwt</c>, symmetric for <c>client_secret_jwt</c>), selected from the assertion's
/// <c>alg</c> header so each assertion is handled exactly once.
/// </summary>
internal abstract class ClientAssertionClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger logger,
    IJwtValidator jwtValidator,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IClientAssertionReplayCache replayCache,
    IClientKeyResolver clientKeyResolver,
    ISecurityProfileCatalog securityProfileCatalog
) : CommonClientAuthenticationHandler(clientResolver, clientFactory, auditEventRecorder, logger)
{
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private IClientAssertionReplayCache ReplayCache { get; } = replayCache;
    private IClientKeyResolver ClientKeyResolver { get; } = clientKeyResolver;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <summary>
    /// When the assertion's signing <paramref name="algorithm"/> belongs to this handler's key family, returns
    /// <see langword="true"/> so this handler owns the assertion; otherwise <see langword="false"/> to defer to the
    /// sibling handler.
    /// </summary>
    protected abstract bool OwnsAlgorithm(SignatureAlgorithm algorithm);

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var httpRequest = openIdContext.Http.Request;

        if (!HttpMethods.IsPost(httpRequest.Method) || !httpRequest.HasFormContentType)
            return ClientAuthenticationResult.Undefined;

        var form = await httpRequest.ReadFormAsync(cancellationToken);

        if (
            !form.TryGetValue(OpenIdConstants.Parameters.ClientAssertion, out var assertionValues)
            || string.IsNullOrEmpty(assertionValues.ToString())
        )
        {
            // No client_assertion present; this handler does not apply.
            return ClientAuthenticationResult.Undefined;
        }

        var assertionType = form.TryGetValue(
            OpenIdConstants.Parameters.ClientAssertionType,
            out var assertionTypeValues
        )
            ? assertionTypeValues.ToString()
            : string.Empty;

        if (
            !string.Equals(
                assertionType,
                OpenIdConstants.ClientAssertionTypes.JwtBearer,
                StringComparison.Ordinal
            )
        )
        {
            // The assertion is not a JWT-bearer assertion; this handler does not apply.
            return ClientAuthenticationResult.Undefined;
        }

        var assertion = assertionValues.ToString();

        // A client assertion is a compact JWS: header.payload.signature (RFC 7515 §7.1).
        var parts = assertion.Split('.');
        if (parts.Length != 3)
            return ClientAuthenticationResult.Undefined;

        JsonElement header;
        JsonElement payload;
        try
        {
            using var headerDoc = JsonDocument.Parse(DecodeBase64Url(parts[0]));
            using var payloadDoc = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            header = headerDoc.RootElement.Clone();
            payload = payloadDoc.RootElement.Clone();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return ClientAuthenticationResult.Undefined;
        }

        // The 'alg' header selects which handler owns the assertion and is never 'none' (RFC 7518 §3.6).
        if (
            !header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var alg)
            || string.IsNullOrEmpty(alg)
            || string.Equals(alg, AlgorithmCodes.DigitalSignature.None, StringComparison.Ordinal)
            || !AlgorithmCollectionProvider.Collection.TryGetSignatureAlgorithm(
                alg,
                out var signatureAlgorithm
            )
            || !OwnsAlgorithm(signatureAlgorithm)
        )
        {
            // Unknown, 'none', or a different key family; let the sibling handler (or none) own it.
            return ClientAuthenticationResult.Undefined;
        }

        // From here the assertion is owned by this handler: any failure is a conclusive invalid_client.
        return await AuthenticateAssertionAsync(
            openIdContext,
            assertion,
            alg,
            payload,
            cancellationToken
        );
    }

    private async ValueTask<ClientAuthenticationResult> AuthenticateAssertionAsync(
        OpenIdContext openIdContext,
        string assertion,
        string alg,
        JsonElement unverifiedPayload,
        CancellationToken cancellationToken
    )
    {
        // OpenID Connect Core §9 requires iss == sub == client_id. The client id is read from the (as-yet-unverified)
        // payload only to locate the client's keys; the signature is verified against those keys below.
        if (
            !unverifiedPayload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Iss, out var iss)
            || !unverifiedPayload.TryGetPropertyValue<string>(
                JoseClaimNames.Payload.Sub,
                out var sub
            )
            || string.IsNullOrEmpty(iss)
            || string.IsNullOrEmpty(sub)
            || !string.Equals(iss, sub, StringComparison.Ordinal)
        )
        {
            return await FailAsync(
                openIdContext,
                "unknown",
                "assertion_iss_sub_mismatch",
                cancellationToken
            );
        }

        var clientId = sub;

        var allowedAlgorithms = openIdContext.Tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported
        );
        if (allowedAlgorithms.Count > 0 && !allowedAlgorithms.Contains(alg, StringComparer.Ordinal))
        {
            return await FailAsync(
                openIdContext,
                clientId,
                "assertion_alg_not_supported",
                cancellationToken
            );
        }

        OpenIdClient? publicClient;
        try
        {
            publicClient = await ClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                clientId,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception
                    is System.Security.Cryptography.CryptographicException
                        or FormatException
                        or InvalidOperationException
            )
        {
            Logger.ClientAuthenticationCredentialDeserializationFailed(clientId, exception);
            return await FailAsync(
                openIdContext,
                clientId,
                "credential_deserialization_failed",
                cancellationToken
            );
        }

        if (publicClient is null)
        {
            Logger.ClientAuthenticationClientNotFound(clientId, openIdContext.Tenant.TenantId);
            return await FailAsync(openIdContext, clientId, "client_not_found", cancellationToken);
        }

        var validAudiences = GetValidAudiences(openIdContext);

        // Honor the client's effective clock_skew setting (the FAPI 2.0 profile tightens this to ≤ 60s via the
        // merge-lattice ceiling); without this the JWT validator would fall back to its 5-minute default.
        var clockSkew = publicClient.Settings.GetValue(OpenIdSettingKeys.ClockSkew);

        // RFC 7523 §3 lets the server reject an assertion valid unreasonably far into the future; the cap also bounds
        // how long its 'jti' occupies the replay cache.
        var maxLifetime = publicClient.Settings.GetValue(
            OpenIdSettingKeys.ClientAssertionMaxLifetime
        );

        // The effective key set includes keys resolved at runtime from the client's jwks_uri (when configured).
        var validationKeys = await ClientKeyResolver.ResolveSecretKeysAsync(
            publicClient,
            cancellationToken
        );

        JwtValidationResult result;
        try
        {
            var parameters = new JwtValidationParameters() { ClockSkew = clockSkew }
                .UseValidationKeys(validationKeys)
                .ValidateIssuer(clientId)
                .ValidateClaim(
                    JoseClaimNames.Payload.Sub,
                    usePayload: true,
                    allowCollection: false,
                    [clientId]
                )
                .ValidateAudience(validAudiences)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime(options =>
                {
                    options.RequireExpirationTime = true;
                    options.MaxLifetime = maxLifetime;
                });

            result = await JwtValidator.ValidateAsync(assertion, parameters, cancellationToken);

            if (!result.IsValid)
                ExceptionDispatchInfo.Throw(result.Exception);

            // Apply any security-profile client-authentication requirements (e.g. FAPI's issuer-only assertion
            // audience). A requirement throws to reject, which the catch below surfaces as an invalid-client failure.
            await SecurityProfileCatalog.EvaluateRequirementsAsync(
                publicClient.Settings,
                new ClientAuthenticationProfileContext(
                    openIdContext,
                    publicClient,
                    result.DecodedJwt.Payload
                ),
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            Logger.ClientAssertionInvalid(clientId, exception);
            return await FailAsync(openIdContext, clientId, "assertion_invalid", cancellationToken);
        }

        var validatedPayload = result.DecodedJwt.Payload;

        // A 'jti' plus an 'exp' are required so the assertion can be rejected on replay (RFC 7523 §3).
        if (
            !validatedPayload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Jti, out var jti)
            || string.IsNullOrEmpty(jti)
            || !validatedPayload.TryGetPropertyNumericDate(
                JoseClaimNames.Payload.Exp,
                out var expiresAt
            )
        )
        {
            return await FailAsync(
                openIdContext,
                clientId,
                "assertion_missing_jti_or_exp",
                cancellationToken
            );
        }

        // Retain the replay record until the assertion can no longer be accepted ('exp' plus the clock skew), saturating
        // rather than overflowing at the edge of the representable range.
        var retentionMargin = clockSkew > TimeSpan.Zero ? clockSkew : TimeSpan.Zero;
        var expiresWhen =
            expiresAt > DateTimeOffset.MaxValue - retentionMargin
                ? DateTimeOffset.MaxValue
                : expiresAt + retentionMargin;

        var recorded = await ReplayCache.TryRecordAsync(
            clientId,
            jti,
            expiresWhen,
            cancellationToken
        );
        if (!recorded)
        {
            Logger.ClientAssertionReplayed(clientId);
            return await FailAsync(
                openIdContext,
                clientId,
                "assertion_replayed",
                cancellationToken
            );
        }

        var confidentialClient = await ClientFactory.CreateConfidentialClientAsync(
            openIdContext,
            publicClient,
            AuthenticationMethod,
            result.DecodedJwt.SecretKey,
            confirmation: null,
            cancellationToken
        );

        return new ClientAuthenticationResult(confidentialClient);
    }

    private async ValueTask<ClientAuthenticationResult> FailAsync(
        OpenIdContext openIdContext,
        string clientId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
            openIdContext,
            clientId,
            reason,
            cancellationToken
        );
        return CreateInvalidClientResult(openIdContext);
    }

    private static IReadOnlyCollection<string> GetValidAudiences(OpenIdContext openIdContext)
    {
        var request = openIdContext.Http.Request;
        var issuer = openIdContext.Tenant.Issuer;

        // OpenID Connect Core §9 accepts either the issuer identifier or the URL of the endpoint receiving the request;
        // a security profile may further restrict this (see IClientAuthenticationProfileRequirement).
        var endpointUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";

        return [issuer, endpointUrl];
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var byteCount = Base64Url.GetByteCountForDecode(value.Length);
        var buffer = new byte[byteCount];
        if (!Base64Url.TryDecode(value, buffer, out var bytesWritten))
            throw new FormatException("The value is not valid base64url.");
        return bytesWritten == byteCount ? buffer : buffer[..bytesWritten];
    }
}
