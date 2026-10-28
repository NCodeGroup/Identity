#region Copyright Preamble

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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Auditing;
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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Refresh Token</c> grant type.
/// </summary>
internal class DefaultRefreshTokenGrantHandler(
    TimeProvider timeProvider,
    IPersistedGrantService persistedGrantService,
    ITokenService tokenService,
    IAuditEventRecorder auditEventRecorder
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ITokenService TokenService { get; } = tokenService;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { OpenIdConstants.GrantTypes.RefreshToken };

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
        var mediator = openIdContext.Mediator;
        var settings = openIdClient.Settings;

        var refreshToken = tokenRequest.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            return errorFactory
                .MissingParameter(OpenIdConstants.Parameters.RefreshToken)
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var persistedGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.RefreshToken,
            refreshToken
        );

        var persistedGrantOrNull = await PersistedGrantService.GetOrDefaultAsync<RefreshTokenGrant>(
            openIdContext,
            persistedGrantId,
            cancellationToken
        );

        if (!persistedGrantOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var persistedGrant = persistedGrantOrNull.Value;
        var isRetry = false;
        if (persistedGrant.Status != PersistedGrantStatus.Active)
        {
            // A revoked grant presented again is a reuse/replay signal (likely rotated away or
            // revoked on suspicion), distinct from a benign expiry.
            if (persistedGrant.Status == PersistedGrantStatus.Revoked)
            {
                if (!IsWithinReuseInterval(settings, persistedGrant, utcNow))
                {
                    return await AnswerReuseAsync(
                        openIdContext,
                        openIdClient,
                        persistedGrant,
                        utcNow,
                        cancellationToken
                    );
                }

                // A rotated token presented again within the reuse interval is a retry, not a reuse. See ADR-0217.
                isRetry = true;
            }
            else
            {
                return errorFactory
                    .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                    .WithStatusCode(StatusCodes.Status400BadRequest);
            }
        }

        var refreshTokenGrant = persistedGrant.Payload;

        // DPoP/mTLS binding continuity (RFC 9449 §5, RFC 8705 §7.1): a sender-constrained grant stays bound to its
        // original key across every rotation, and the presented proof must match that key.
        var storedConfirmation = refreshTokenGrant.Confirmation;
        ProofOfPossessionBinding effectiveBinding;
        if (storedConfirmation.HasValue)
        {
            var presented = proofOfPossessionBinding.Confirmation;
            var matches = !string.IsNullOrEmpty(storedConfirmation.JwkThumbprint)
                ? string.Equals(
                    presented.JwkThumbprint,
                    storedConfirmation.JwkThumbprint,
                    StringComparison.Ordinal
                )
                : string.Equals(
                    presented.X509CertificateSha256Thumbprint,
                    storedConfirmation.X509CertificateSha256Thumbprint,
                    StringComparison.Ordinal
                );

            if (!matches)
            {
                var errorCode = !string.IsNullOrEmpty(storedConfirmation.JwkThumbprint)
                    ? OpenIdProtocol.ErrorCodes.InvalidDPoPProof
                    : OpenIdProtocol.ErrorCodes.InvalidGrant;
                return errorFactory
                    .Create(errorCode)
                    .WithDescription(
                        "The presented proof-of-possession does not match the refresh token's bound key."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest);
            }

            // Rotate against the stored confirmation (authoritative), not the freshly computed one.
            effectiveBinding = proofOfPossessionBinding with
            {
                Confirmation = storedConfirmation,
            };
        }
        else
        {
            effectiveBinding = proofOfPossessionBinding;
        }

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<RefreshTokenGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                refreshTokenGrant
            ),
            cancellationToken
        );

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = utcNow,
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.RefreshToken,
            OriginalScopes = refreshTokenGrant.OriginalScopes,
            EffectiveScopes = tokenRequest.Scopes ?? refreshTokenGrant.OriginalScopes,
            Resources = tokenRequest.Resources,
            RefreshToken = refreshToken,
            RefreshTokenLineageId = persistedGrant.RefreshTokenLineageId,
            SubjectAuthentication = refreshTokenGrant.SubjectAuthentication,
            Confirmation = effectiveBinding.Confirmation,
        };

        var rotates =
            isRetry
            || RefreshTokenRotationRequirement.IsRequired(
                openIdClient,
                refreshTokenGrant.Confirmation
            );
        var issuesRefreshToken = securityTokenRequest.EffectiveScopes.Contains(
            OpenIdConstants.ScopeTypes.OfflineAccess
        );

        string? responseRefreshToken = null;
        if (isRetry)
        {
            responseRefreshToken = await ReissueRefreshTokenForRetryAsync(
                openIdContext,
                openIdClient,
                persistedGrant,
                securityTokenRequest,
                issuesRefreshToken,
                cancellationToken
            );

            if (responseRefreshToken is null)
            {
                return errorFactory
                    .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                    .WithStatusCode(StatusCodes.Status400BadRequest);
            }
        }
        else if (rotates && issuesRefreshToken)
        {
            var successor = await TokenService.RotateRefreshTokenAsync(
                openIdContext,
                openIdClient,
                persistedGrantId,
                securityTokenRequest,
                cancellationToken
            );

            if (successor is { } rotated)
            {
                responseRefreshToken = rotated.TokenValue;
            }
            else if (
                await WasRotatedByConcurrentRequestAsync(
                    openIdContext,
                    settings,
                    persistedGrantId,
                    utcNow,
                    cancellationToken
                )
            )
            {
                // A rotation that lost to a concurrent rotation is a retry while the reuse interval lasts: the winner
                // consumed the grant a moment ago. See ADR-0217.
                responseRefreshToken = await ReissueRefreshTokenForRetryAsync(
                    openIdContext,
                    openIdClient,
                    persistedGrant,
                    securityTokenRequest,
                    issuesRefreshToken,
                    cancellationToken
                );

                if (responseRefreshToken is null)
                {
                    return errorFactory
                        .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                        .WithStatusCode(StatusCodes.Status400BadRequest);
                }
            }
            else
            {
                return await AnswerReuseAsync(
                    openIdContext,
                    openIdClient,
                    persistedGrant,
                    utcNow,
                    cancellationToken
                );
            }
        }
        else if (rotates)
        {
            var spent = await PersistedGrantService.TryRevokeOnceAsync(
                openIdContext,
                persistedGrantId,
                utcNow,
                cancellationToken
            );

            if (!spent)
            {
                return await AnswerReuseAsync(
                    openIdContext,
                    openIdClient,
                    persistedGrant,
                    utcNow,
                    cancellationToken
                );
            }
        }
        else if (issuesRefreshToken)
        {
            responseRefreshToken = refreshToken;
        }

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            securityTokenRequest,
            effectiveBinding.TokenType,
            responseRefreshToken,
            cancellationToken
        );

        var expirationPolicy = settings.GetValue(OpenIdSettingKeys.RefreshTokenExpirationPolicy);
        var useSlidingExpiration =
            expirationPolicy == OpenIdConstants.RefreshTokenExpirationPolicy.Sliding;
        if (useSlidingExpiration && !rotates)
        {
            var lifetime = settings.GetValue(OpenIdSettingKeys.RefreshTokenLifetime);
            var expiresWhen = utcNow + lifetime;

            await PersistedGrantService.UpdateExpirationAsync(
                openIdContext,
                persistedGrantId,
                expiresWhen,
                cancellationToken
            );
        }

        return tokenResponse;
    }

    // A grant that a rotation spent is a retry, not a reuse, while its reuse interval lasts and the grant has not
    // expired. A grant that was revoked otherwise is a reuse. See ADR-0217.
    private static bool IsWithinReuseInterval(
        IReadOnlySettingCollection settings,
        PersistedGrant<RefreshTokenGrant> grant,
        DateTimeOffset utcNow
    )
    {
        if (
            grant.ConsumedWhen is not { } rotatedWhen
            || (grant.ExpiresWhen is { } expiresWhen && expiresWhen <= utcNow)
        )
        {
            return false;
        }

        return utcNow
            < rotatedWhen + settings.GetValue(OpenIdSettingKeys.RefreshTokenReuseInterval);
    }

    // A retry is answered only when it can issue another refresh token in a lineage that is still active. Otherwise it
    // is refused (null) without a replay audit and without revoking the lineage. A retry that is answered is audited as
    // a retry; a refused one is not. See ADR-0217.
    private async ValueTask<string?> ReissueRefreshTokenForRetryAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        PersistedGrant<RefreshTokenGrant> persistedGrant,
        CreateSecurityTokenRequest securityTokenRequest,
        bool issuesRefreshToken,
        CancellationToken cancellationToken
    )
    {
        if (!issuesRefreshToken || string.IsNullOrEmpty(persistedGrant.RefreshTokenLineageId))
            return null;

        var reissued = await TokenService.ReissueRefreshTokenAsync(
            openIdContext,
            openIdClient,
            securityTokenRequest,
            cancellationToken
        );

        if (reissued is not { } successor)
            return null;

        await AuditEventRecorder.RecordRefreshTokenRetryAsync(
            openIdContext,
            openIdClient,
            persistedGrant.Payload.SubjectAuthentication?.PrincipalId,
            cancellationToken
        );

        return successor.TokenValue;
    }

    // A rotation that lost its spend was won by a concurrent rotation when the grant, read again, is a rotated grant
    // that is within the reuse interval. The grant is not read again when the interval is off. See ADR-0217.
    private async ValueTask<bool> WasRotatedByConcurrentRequestAsync(
        OpenIdContext openIdContext,
        IReadOnlySettingCollection settings,
        PersistedGrantId persistedGrantId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken
    )
    {
        if (settings.GetValue(OpenIdSettingKeys.RefreshTokenReuseInterval) <= TimeSpan.Zero)
            return false;

        var currentOrNull = await PersistedGrantService.GetOrDefaultAsync<RefreshTokenGrant>(
            openIdContext,
            persistedGrantId,
            cancellationToken
        );

        if (!currentOrNull.HasValue)
            return false;

        var current = currentOrNull.Value;
        return current.Status == PersistedGrantStatus.Revoked
            && IsWithinReuseInterval(settings, current, utcNow);
    }

    // A refresh token presented after it was rotated or revoked, or spent first by a concurrent request, is a reuse
    // (RFC 9700 §4.14.2). See ADR-0211.
    private async ValueTask<IOpenIdResponse> AnswerReuseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        PersistedGrant<RefreshTokenGrant> persistedGrant,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        await AuditEventRecorder.RecordRefreshTokenReplayAsync(
            openIdContext,
            openIdClient,
            persistedGrant.Payload.SubjectAuthentication?.PrincipalId,
            cancellationToken
        );

        var reusePolicy = settings.GetValue(OpenIdSettingKeys.RefreshTokenReusePolicy);
        var lineageId = persistedGrant.RefreshTokenLineageId;
        if (
            reusePolicy == OpenIdConstants.RefreshTokenReusePolicy.RevokeAll
            && !string.IsNullOrEmpty(lineageId)
        )
        {
            // Reuse detection: revoke the whole rotation family so a stolen token cannot be
            // exchanged even if the attacker raced ahead of the legitimate client (RFC 9700).
            await PersistedGrantService.RevokeByRefreshTokenLineageAsync(
                openIdContext,
                lineageId,
                utcNow,
                cancellationToken
            );
        }

        return errorFactory
            .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
            .WithStatusCode(StatusCodes.Status400BadRequest);
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest securityTokenRequest,
        string tokenType,
        string? refreshToken,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = securityTokenRequest.EffectiveScopes.ToList();

        {
            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken
            );

            tokenResponse.AccessToken = securityToken.TokenValue;
            tokenResponse.ExpiresIn = securityToken.TokenLifetime.Duration;
            tokenResponse.TokenType = tokenType;
        }

        if (securityTokenRequest.EffectiveScopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
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

        tokenResponse.RefreshToken = refreshToken;

        return tokenResponse;
    }
}
