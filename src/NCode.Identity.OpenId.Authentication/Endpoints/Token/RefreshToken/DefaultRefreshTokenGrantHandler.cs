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
        if (persistedGrant.Status != PersistedGrantStatus.Active)
        {
            // A revoked grant presented again is a reuse/replay signal (likely rotated away or
            // revoked on suspicion), distinct from a benign expiry.
            if (persistedGrant.Status == PersistedGrantStatus.Revoked)
            {
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
            }

            return errorFactory
                .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
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
                    ? OpenIdConstants.ErrorCodes.InvalidDPoPProof
                    : OpenIdConstants.ErrorCodes.InvalidGrant;
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

        var rotationEnabled = settings.GetValue(OpenIdSettingKeys.RefreshTokenRotationEnabled);
        if (rotationEnabled)
        {
            await PersistedGrantService.SetRevokedOnceAsync(
                openIdContext,
                persistedGrantId,
                utcNow,
                cancellationToken
            );
        }

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            refreshTokenGrant,
            persistedGrant.RefreshTokenLineageId,
            effectiveBinding,
            cancellationToken
        );

        var expirationPolicy = settings.GetValue(OpenIdSettingKeys.RefreshTokenExpirationPolicy);
        var useSlidingExpiration =
            expirationPolicy == OpenIdConstants.RefreshTokenExpirationPolicy.Sliding;
        if (useSlidingExpiration && !rotationEnabled)
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

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        RefreshTokenGrant refreshTokenGrant,
        string? refreshTokenLineageId,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var (_, originalScopes, _, subjectAuthentication, _) = refreshTokenGrant;

        var effectiveScopes = tokenRequest.Scopes ?? originalScopes;

        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = effectiveScopes.ToList();

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.RefreshToken,
            OriginalScopes = originalScopes,
            EffectiveScopes = effectiveScopes,
            Resources = tokenRequest.Resources,
            RefreshToken = tokenRequest.RefreshToken,
            RefreshTokenLineageId = refreshTokenLineageId,
            SubjectAuthentication = subjectAuthentication,
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

        if (effectiveScopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
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

        if (effectiveScopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
        {
            var settings = openIdClient.Settings;
            var rotationEnabled = settings.GetValue(OpenIdSettingKeys.RefreshTokenRotationEnabled);

            if (rotationEnabled)
            {
                var newRequest = securityTokenRequest with
                {
                    AccessToken = tokenResponse.AccessToken,
                };

                var securityToken = await TokenService.CreateRefreshTokenAsync(
                    openIdContext,
                    openIdClient,
                    newRequest,
                    cancellationToken
                );

                tokenResponse.RefreshToken = securityToken.TokenValue;
            }
            else
            {
                tokenResponse.RefreshToken = tokenRequest.RefreshToken;
            }
        }

        return tokenResponse;
    }
}
