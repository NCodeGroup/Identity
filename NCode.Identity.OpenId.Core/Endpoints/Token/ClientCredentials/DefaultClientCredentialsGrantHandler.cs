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
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.ClientCredentials;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Client Credentials</c> grant type.
/// </summary>
public class DefaultClientCredentialsGrantHandler(
    TimeProvider timeProvider,
    IOpenIdErrorFactory errorFactory,
    ITokenService tokenService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        OpenIdConstants.GrantTypes.ClientCredentials
    };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;

        if (!openIdClient.IsConfidential)
            return ErrorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var confidentialClient = openIdClient.ConfidentialClient;
        var clientCredentialsGrant = new ClientCredentialsGrant(confidentialClient);

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<ClientCredentialsGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                clientCredentialsGrant),
            cancellationToken);

        var tokenResponse = await CreateTokenResponseAsync(
            openIdContext,
            confidentialClient,
            tokenRequest,
            cancellationToken);

        return tokenResponse;
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdConfidentialClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var scopes = tokenRequest.Scopes;
        Debug.Assert(scopes is not null);

        var openIdEnvironment = openIdContext.Environment;
        var tokenResponse = TokenResponse.Create(openIdEnvironment);

        tokenResponse.Scopes = scopes;

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            GrantType = tokenRequest.GrantType ?? OpenIdConstants.GrantTypes.ClientCredentials,
            OriginalScopes = scopes,
            EffectiveScopes = scopes
        };

        var securityToken = await TokenService.CreateAccessTokenAsync(
            openIdContext,
            openIdClient,
            securityTokenRequest,
            cancellationToken);

        tokenResponse.AccessToken = securityToken.TokenValue;
        tokenResponse.ExpiresIn = securityToken.TokenPeriod.Duration;
        tokenResponse.TokenType = OpenIdConstants.TokenTypes.Bearer; // TODO: add support for DPoP

        Debug.Assert(!scopes.Contains(OpenIdConstants.ScopeTypes.OpenId));
        Debug.Assert(!scopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess));

        return tokenResponse;
    }
}
