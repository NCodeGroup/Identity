﻿#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Diagnostics;
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
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.AuthorizationCode;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Authorization Code</c> grant type.
/// </summary>
public class DefaultAuthorizationCodeGrantHandler(
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
        OpenIdConstants.GrantTypes.AuthorizationCode,
        OpenIdConstants.GrantTypes.Hybrid
    };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var errorFactory = openIdContext.ErrorFactory;
        var mediator = openIdContext.Mediator;

        var authorizationCode = tokenRequest.AuthorizationCode;
        if (string.IsNullOrEmpty(authorizationCode))
            return errorFactory
                .MissingParameter(OpenIdConstants.Parameters.AuthorizationCode)
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var grantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            GrantKey = authorizationCode
        };

        var persistedGrantOrNull = await PersistedGrantService.TryConsumeOnce<AuthorizationGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (!persistedGrantOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided authorization code is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var persistedGrant = persistedGrantOrNull.Value;
        Debug.Assert(persistedGrant.Status == PersistedGrantStatus.Active);

        var authorizationGrant = persistedGrant.Payload;

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<AuthorizationGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                authorizationGrant),
            cancellationToken);

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            authorizationGrant,
            cancellationToken);

        return tokenResponse;
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        AuthorizationGrant authorizationGrant,
        CancellationToken cancellationToken)
    {
        var openIdEnvironment = openIdContext.Environment;
        var (authorizationRequest, subjectAuthentication) = authorizationGrant;

        var originalScopes = authorizationRequest.Scopes;
        var effectiveScopes = tokenRequest.Scopes ?? originalScopes;

        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = effectiveScopes.ToList();

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.AuthorizationCode,
            Nonce = authorizationRequest.Nonce,
            State = authorizationRequest.State,
            OriginalScopes = originalScopes,
            EffectiveScopes = effectiveScopes,
            AuthorizationCode = tokenRequest.AuthorizationCode,
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

            var securityToken = await TokenService.CreateIdTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken);

            tokenResponse.IdToken = securityToken.TokenValue;
        }

        if (effectiveScopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
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

        return tokenResponse;
    }
}
