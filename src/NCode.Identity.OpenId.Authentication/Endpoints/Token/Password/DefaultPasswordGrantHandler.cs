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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Password</c> grant type.
/// </summary>
internal class DefaultPasswordGrantHandler(TimeProvider timeProvider, ITokenService tokenService)
    : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { OpenIdConstants.GrantTypes.Password };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        ProofOfPossessionBinding proofOfPossessionBinding,
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

        // Capture the authentication instant once at the start of the flow and carry it forward as the ticket's
        // IssuedUtc (which drives the subject's `auth_time`) and the issued token's `iat`, so the ROPC path records the
        // same authentication metadata as the interactive and bearer entry points instead of dropping it.
        var authenticatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var authenticationProperties = new AuthenticationProperties
        {
            IssuedUtc = authenticatedWhen,
        };

        var disposition = await mediator.SendAsync<
            AuthenticatePasswordGrantCommand,
            AuthenticateSubjectDisposition
        >(
            new AuthenticatePasswordGrantCommand(
                openIdContext,
                openIdClient,
                tokenRequest,
                authenticationProperties
            ),
            cancellationToken
        );

        if (!disposition.IsAuthenticated)
        {
            return disposition.Error
                ?? errorFactory
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
            authenticatedWhen,
            proofOfPossessionBinding,
            cancellationToken
        );

        return tokenResponse;
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        PasswordGrant passwordGrant,
        DateTimeOffset createdWhen,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var scopes = tokenRequest.Scopes ?? [];

        var openIdEnvironment = openIdContext.Environment;
        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = scopes;

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = createdWhen,
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.Password,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            Resources = tokenRequest.Resources,
            SubjectAuthentication = passwordGrant.SubjectAuthentication,
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

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
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

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
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
}
