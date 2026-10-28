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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Device Code</c> grant type
/// (RFC 8628 §3.4–3.5).
/// </summary>
internal class DefaultDeviceCodeGrantHandler(
    TimeProvider timeProvider,
    IPersistedGrantService persistedGrantService,
    ITokenService tokenService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { OpenIdProtocol.GrantTypes.DeviceCode };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        var deviceCode = tokenRequest.DeviceCode;
        if (string.IsNullOrEmpty(deviceCode))
            return errorFactory
                .MissingParameter(OpenIdProtocol.Parameters.DeviceCode)
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var deviceCodeGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.DeviceCode,
            deviceCode
        );

        var persistedGrantOrNull = await PersistedGrantService.GetOrDefaultAsync<DeviceCodeGrant>(
            openIdContext,
            deviceCodeGrantId,
            cancellationToken
        );

        if (!persistedGrantOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided device code is invalid.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var persistedGrant = persistedGrantOrNull.Value;

        // A device code is redeemed only by the client it was issued to (RFC 8628 §3.4). See ADR-0225.
        if (
            !string.Equals(
                persistedGrant.Payload.ClientId,
                openIdClient.ClientId,
                StringComparison.Ordinal
            )
        )
            return errorFactory
                .InvalidGrant("The provided device code was issued to a different client.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        if (persistedGrant.Status == PersistedGrantStatus.Expired)
            return Error(
                errorFactory,
                OpenIdProtocol.ErrorCodes.ExpiredToken,
                "The device code has expired."
            );

        if (persistedGrant.Status != PersistedGrantStatus.Active)
            return errorFactory
                .InvalidGrant("The provided device code is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var payload = persistedGrant.Payload;

        switch (payload.Status)
        {
            case DeviceAuthorizationStatus.Denied:
                return Error(
                    errorFactory,
                    OpenIdProtocol.ErrorCodes.AccessDenied,
                    "The end user denied the device authorization request."
                );

            case DeviceAuthorizationStatus.Pending:
                return await HandlePendingAsync(
                    openIdContext,
                    errorFactory,
                    settings,
                    deviceCodeGrantId,
                    payload,
                    cancellationToken
                );

            case DeviceAuthorizationStatus.Approved:
                return await HandleApprovedAsync(
                    openIdContext,
                    openIdClient,
                    errorFactory,
                    tokenRequest,
                    deviceCodeGrantId,
                    payload,
                    proofOfPossessionBinding,
                    cancellationToken
                );

            default:
                return errorFactory
                    .InvalidGrant("The provided device code is invalid.")
                    .WithStatusCode(StatusCodes.Status400BadRequest);
        }
    }

    private async ValueTask<IOpenIdResponse> HandlePendingAsync(
        OpenIdContext openIdContext,
        IOpenIdErrorFactory errorFactory,
        IReadOnlySettingCollection settings,
        PersistedGrantId deviceCodeGrantId,
        DeviceCodeGrant payload,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var interval = settings.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval);

        var tooFast =
            payload.LastPolledWhen is { } lastPolledWhen && utcNow - lastPolledWhen < interval;

        await PersistedGrantService.UpdatePayloadAsync<DeviceCodeGrant>(
            openIdContext,
            deviceCodeGrantId,
            stored => RecordPoll(stored, utcNow),
            cancellationToken
        );

        return tooFast
            ? Error(
                errorFactory,
                OpenIdProtocol.ErrorCodes.SlowDown,
                "The device is polling faster than the permitted interval."
            )
            : Error(
                errorFactory,
                OpenIdProtocol.ErrorCodes.AuthorizationPending,
                "The end user has not yet completed the device authorization request."
            );
    }

    private static PersistedGrantUpdate<DeviceCodeGrant>? RecordPoll(
        PersistedGrant<DeviceCodeGrant> stored,
        DateTimeOffset utcNow
    )
    {
        // An approval that lands between this poll's read and its write stands. See ADR-0222.
        if (stored.Payload.Status != DeviceAuthorizationStatus.Pending)
            return null;

        return new PersistedGrantUpdate<DeviceCodeGrant>
        {
            Payload = stored.Payload with { LastPolledWhen = utcNow },
        };
    }

    private async ValueTask<IOpenIdResponse> HandleApprovedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdErrorFactory errorFactory,
        ITokenRequest tokenRequest,
        PersistedGrantId deviceCodeGrantId,
        DeviceCodeGrant payload,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        // The subject that approved the request is validated before the device code is spent, so a refused subject
        // leaves it unspent. See ADR-0225.
        var validateCommand = new ValidateTokenGrantCommand<DeviceCodeGrant>(
            openIdContext,
            openIdClient,
            tokenRequest,
            payload
        );
        await openIdContext.Mediator.SendAsync(validateCommand, cancellationToken);

        // Claim the approved request exactly once so a device_code is redeemed for tokens a single time.
        var consumedOrNull = await PersistedGrantService.ConsumeOnceOrDefault<DeviceCodeGrant>(
            openIdContext,
            deviceCodeGrantId,
            cancellationToken
        );

        if (!consumedOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided device code has already been redeemed.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var consumedPayload = consumedOrNull.Value.Payload;

        return await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            consumedPayload,
            proofOfPossessionBinding,
            cancellationToken
        );
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        DeviceCodeGrant payload,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        var originalScopes = payload.Scopes;
        var effectiveScopes = tokenRequest.Scopes ?? originalScopes;

        var tokenResponse = TokenResponse.Create(openIdEnvironment);
        tokenResponse.Scopes = effectiveScopes.ToList();

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdProtocol.GrantTypes.DeviceCode,
            OriginalScopes = originalScopes,
            EffectiveScopes = effectiveScopes,
            Resources = tokenRequest.Resources,
            SubjectAuthentication = payload.SubjectAuthentication,
            Confirmation = proofOfPossessionBinding.Confirmation,
        };

        {
            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken
            );

            tokenResponse.AccessToken = securityToken.TokenValue;
            tokenResponse.ExpiresIn = securityToken.TokenLifetime.Duration;
            tokenResponse.TokenType = proofOfPossessionBinding.TokenType;
        }

        if (effectiveScopes.Contains(OpenIdProtocol.ScopeTypes.OpenId))
        {
            var newRequest = securityTokenRequest with { AccessToken = tokenResponse.AccessToken };

            var securityToken = await TokenService.CreateIdTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken
            );

            tokenResponse.IdToken = securityToken.TokenValue;
        }

        if (effectiveScopes.Contains(OpenIdProtocol.ScopeTypes.OfflineAccess))
        {
            var newRequest = securityTokenRequest with { AccessToken = tokenResponse.AccessToken };

            var securityToken = await TokenService.CreateRefreshTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken
            );

            tokenResponse.RefreshToken = securityToken.TokenValue;
        }

        return tokenResponse;
    }

    private static IOpenIdError Error(
        IOpenIdErrorFactory errorFactory,
        string errorCode,
        string description
    ) =>
        errorFactory
            .Create(errorCode)
            .WithDescription(description)
            .WithStatusCode(StatusCodes.Status400BadRequest);
}
