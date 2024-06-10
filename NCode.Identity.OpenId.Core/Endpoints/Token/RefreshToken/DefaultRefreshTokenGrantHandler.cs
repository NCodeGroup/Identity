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
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.RefreshToken;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Refresh Token</c> grant type.
/// </summary>
public class DefaultRefreshTokenGrantHandler(
    TimeProvider timeProvider,
    OpenIdServer openIdServer,
    IOpenIdErrorFactory errorFactory,
    IPersistedGrantService persistedGrantService,
    ITokenService tokenService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ITokenService TokenService { get; } = tokenService;

    private IOpenIdError InvalidGrantError => ErrorFactory
        .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        OpenIdConstants.GrantTypes.RefreshToken
    };

    /// <inheritdoc />
    public async ValueTask<ITokenResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var settings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;

        var refreshToken = tokenRequest.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.RefreshToken)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var grantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
            GrantKey = refreshToken
        };

        var persistedGrantOrNull = await PersistedGrantService.TryGetAsync<RefreshTokenGrant>(
            grantId,
            cancellationToken);

        if (!persistedGrantOrNull.HasValue)
            throw InvalidGrantError.AsException("The refresh token was not found.");

        var persistedGrant = persistedGrantOrNull.Value;
        if (persistedGrant.Status != PersistedGrantStatus.Active)
        {
            // TODO: refresh_token_reuse_policy (none, revoke_all)
            throw InvalidGrantError.AsException("The refresh token is not active.");
        }

        var refreshTokenGrant = persistedGrant.Payload;

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<RefreshTokenGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                refreshTokenGrant),
            cancellationToken);

        var rotationEnabled = settings.RefreshTokenRotationEnabled;
        if (rotationEnabled)
        {
            await PersistedGrantService.SetRevokedAsync(
                grantId,
                utcNow,
                cancellationToken);
        }

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            refreshTokenGrant,
            cancellationToken);

        var expirationPolicy = settings.RefreshTokenExpirationPolicy;
        var useSlidingExpiration = expirationPolicy == OpenIdConstants.RefreshTokenExpirationPolicy.Sliding;
        if (useSlidingExpiration && !rotationEnabled)
        {
            var lifetime = settings.RefreshTokenLifetime;
            var expiresWhen = utcNow + lifetime;

            await PersistedGrantService.UpdateExpirationAsync(
                grantId,
                expiresWhen,
                cancellationToken);
        }

        return tokenResponse;
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        RefreshTokenGrant refreshTokenGrant,
        CancellationToken cancellationToken)
    {
        var (_, originalScopes, _, subjectAuthentication) = refreshTokenGrant;

        var effectiveScopes = tokenRequest.Scopes ?? originalScopes;

        var tokenResponse = TokenResponse.Create(OpenIdServer);

        tokenResponse.Scopes = effectiveScopes;

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
                cancellationToken);

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

            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken);

            tokenResponse.IdToken = securityToken.TokenValue;
        }

        if (effectiveScopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
        {
            var settings = openIdClient.Settings;
            var rotationEnabled = settings.RefreshTokenRotationEnabled;

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
                    cancellationToken);

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
