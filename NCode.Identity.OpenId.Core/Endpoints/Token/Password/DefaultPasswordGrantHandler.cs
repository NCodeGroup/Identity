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
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Password</c> grant type.
/// </summary>
public class DefaultPasswordGrantHandler(
    TimeProvider timeProvider,
    ITokenService tokenService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        OpenIdConstants.GrantTypes.Password
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

        var username = tokenRequest.Username;
        if (string.IsNullOrEmpty(username))
        {
            return errorFactory
                .MissingParameter(OpenIdConstants.Parameters.Username)
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var disposition = await mediator.SendAsync<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>(
            new AuthenticatePasswordGrantCommand(
                openIdContext,
                openIdClient,
                tokenRequest
            ),
            cancellationToken
        );

        if (!disposition.IsAuthenticated)
        {
            return disposition.Error ??
                   errorFactory
                       .InvalidGrant("The provided credentials are invalid, expired, or revoked.")
                       .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var subjectAuthentication = disposition.Ticket.Value;

        var passwordGrant = new PasswordGrant(subjectAuthentication);

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<PasswordGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                passwordGrant
            ),
            cancellationToken
        );

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            passwordGrant,
            cancellationToken
        );

        return tokenResponse;
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        PasswordGrant passwordGrant,
        CancellationToken cancellationToken
    )
    {
        var scopes = tokenRequest.Scopes;
        Debug.Assert(scopes is not null);

        var openIdEnvironment = openIdContext.Environment;
        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = scopes;

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.Password,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            SubjectAuthentication = passwordGrant.SubjectAuthentication
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

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
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
                cancellationToken
            );

            tokenResponse.RefreshToken = securityToken.TokenValue;
        }

        return tokenResponse;
    }
}
