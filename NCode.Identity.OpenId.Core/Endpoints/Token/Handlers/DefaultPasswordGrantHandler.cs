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

using System.Diagnostics;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Password</c> grant type.
/// </summary>
public class DefaultPasswordGrantHandler(
    TimeProvider timeProvider,
    OpenIdServer openIdServer,
    IOpenIdErrorFactory errorFactory,
    ITokenService tokenService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        OpenIdConstants.GrantTypes.Password
    };

    /// <inheritdoc />
    public async ValueTask<ITokenResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;

        var subjectAuthentication = await mediator.SendAsync<AuthenticatePasswordGrantCommand, SubjectAuthentication>(
            new AuthenticatePasswordGrantCommand(
                openIdContext,
                openIdClient,
                tokenRequest),
            cancellationToken);

        var passwordGrant = new PasswordGrant(subjectAuthentication);

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<PasswordGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                passwordGrant),
            cancellationToken);

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            passwordGrant,
            cancellationToken);

        return tokenResponse;
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        PasswordGrant passwordGrant,
        CancellationToken cancellationToken)
    {
        var scopes = tokenRequest.Scopes;
        Debug.Assert(scopes is not null);

        var tokenResponse = TokenResponse.Create(OpenIdServer);

        tokenResponse.Scopes = scopes;

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.Password,
            Scopes = tokenRequest.Scopes,
            SubjectAuthentication = passwordGrant.SubjectAuthentication
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

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
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

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
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
