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

using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.RefreshToken;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Refresh Token</c> grant type.
/// </summary>
public class DefaultRefreshTokenGrantHandler(
    TimeProvider timeProvider,
    IPersistedGrantService persistedGrantService,
    ITokenService tokenService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        OpenIdConstants.GrantTypes.RefreshToken
    };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken
    )
    {
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

        var grantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
            GrantKey = refreshToken
        };

        var persistedGrantOrNull = await PersistedGrantService.TryGetAsync<RefreshTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (!persistedGrantOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var persistedGrant = persistedGrantOrNull.Value;
        if (persistedGrant.Status != PersistedGrantStatus.Active)
            // TODO: refresh_token_reuse_policy (none, revoke_all)
            return errorFactory
                .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var refreshTokenGrant = persistedGrant.Payload;

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<RefreshTokenGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                refreshTokenGrant
            ),
            cancellationToken
        );

        var rotationEnabled = settings.GetValue(SettingKeys.RefreshTokenRotationEnabled);
        if (rotationEnabled)
        {
            await PersistedGrantService.SetRevokedAsync(
                openIdContext,
                grantId,
                utcNow,
                cancellationToken
            );
        }

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            refreshTokenGrant,
            cancellationToken
        );

        var expirationPolicy = settings.GetValue(SettingKeys.RefreshTokenExpirationPolicy);
        var useSlidingExpiration = expirationPolicy == OpenIdConstants.RefreshTokenExpirationPolicy.Sliding;
        if (useSlidingExpiration && !rotationEnabled)
        {
            var lifetime = settings.GetValue(SettingKeys.RefreshTokenLifetime);
            var expiresWhen = utcNow + lifetime;

            await PersistedGrantService.UpdateExpirationAsync(
                openIdContext,
                grantId,
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
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var (_, originalScopes, _, subjectAuthentication) = refreshTokenGrant;

        var effectiveScopes = tokenRequest.Scopes ?? originalScopes;

        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = effectiveScopes.ToList();

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.RefreshToken,
            OriginalScopes = originalScopes,
            EffectiveScopes = effectiveScopes,
            RefreshToken = tokenRequest.RefreshToken,
            SubjectAuthentication = subjectAuthentication
        };

        {
            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken
            );

            tokenResponse.AccessToken = securityToken.TokenValue;
            tokenResponse.ExpiresIn = securityToken.TokenPeriod.Duration;
            tokenResponse.TokenType = OpenIdConstants.TokenTypes.Bearer; // TODO: add support for DPoP
        }

        if (effectiveScopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
        {
            var newRequest = securityTokenRequest with
            {
                AccessToken = tokenResponse.AccessToken
            };

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
            var rotationEnabled = settings.GetValue(SettingKeys.RefreshTokenRotationEnabled);

            if (rotationEnabled)
            {
                var newRequest = securityTokenRequest with
                {
                    AccessToken = tokenResponse.AccessToken
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
