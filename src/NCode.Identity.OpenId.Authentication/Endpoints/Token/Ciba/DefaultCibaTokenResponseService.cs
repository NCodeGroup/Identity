// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;

/// <summary>
/// Provides the default implementation of <see cref="ICibaTokenResponseService"/>.
/// </summary>
internal sealed class DefaultCibaTokenResponseService(
    TimeProvider timeProvider,
    ITokenService tokenService
) : ICibaTokenResponseService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public async ValueTask<ITokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CibaGrant payload,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        // CIBA does not allow the client to narrow scope at the token endpoint; the granted scopes are authoritative.
        var scopes = payload.Scopes;

        var tokenResponse = TokenResponse.Create(openIdEnvironment);
        tokenResponse.Scopes = scopes.ToList();

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = OpenIdProtocol.GrantTypes.Ciba,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            Resources = payload.Resources,
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

        if (scopes.Contains(OpenIdProtocol.ScopeTypes.OpenId))
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

        if (scopes.Contains(OpenIdProtocol.ScopeTypes.OfflineAccess))
        {
            var newRequest = securityTokenRequest with { AccessToken = tokenResponse.AccessToken };

            var securityToken = await TokenService.CreateRefreshTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken
            );

            tokenResponse.SetRefreshToken(securityToken);
        }

        return tokenResponse;
    }
}
