#region Copyright Preamble

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
using NCode.CryptoMemory;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Token.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Authorization Code</c> grant type.
/// </summary>
public class DefaultAuthorizationCodeGrantHandler(
    TimeProvider timeProvider,
    OpenIdServer openIdServer,
    ICryptoService cryptoService,
    IOpenIdErrorFactory errorFactory,
    IPersistedGrantService persistedGrantService,
    ITokenService tokenService
) : ITokenGrantHandler,
    ICommandHandler<ValidateTokenGrantCommand<AuthorizationGrant>>
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public string GrantType => OpenIdConstants.GrantTypes.AuthorizationCode;

    /// <inheritdoc />
    public async ValueTask<ITokenResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;

        var authorizationCode = tokenRequest.Code;
        if (string.IsNullOrEmpty(authorizationCode))
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.Code)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        var grantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            GrantKey = authorizationCode
        };

        var persistedGrant = await PersistedGrantService.TryGetAsync<AuthorizationGrant>(
            grantId,
            singleUse: true,
            setConsumed: true,
            cancellationToken);

        if (!persistedGrant.HasValue)
            throw ErrorFactory
                .InvalidGrant("The provided authorization code is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        var authorizationGrant = persistedGrant.Value.Payload;

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

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenGrantCommand<AuthorizationGrant> command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, tokenRequest, authorizationGrant) = command;
        var (authorizationRequest, _) = authorizationGrant;

        // client_id from authorization request
        if (!string.Equals(openIdClient.ClientId, authorizationRequest.ClientId, StringComparison.Ordinal))
        {
            throw ErrorFactory
                .InvalidGrant("The provided authorization code was issued to another client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // client_id from token request
        if (!string.IsNullOrEmpty(tokenRequest.ClientId) &&
            !string.Equals(openIdClient.ClientId, tokenRequest.ClientId, StringComparison.Ordinal))
        {
            // invalid_request
            throw ErrorFactory
                .InvalidRequest("The 'client_id' parameter, when specified, must be identical to the authenticated client identifier.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // redirect_uri from token request
        var redirectUri = tokenRequest.RedirectUri;
        if (redirectUri is null)
        {
            // invalid_request
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // redirect_uri from authorization request
        if (authorizationRequest.RedirectUri != redirectUri)
        {
            // invalid_grant
            throw ErrorFactory
                .InvalidGrant("The provided redirect uri does not match the authorization request.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // scope
        var scopeCount = tokenRequest.Scopes?.Count ?? 0;
        if (scopeCount == 0)
        {
            // invalid_request
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.Scope)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // TODO: validate resource

        // TODO: validate actual scope values

        // TODO: validate user/principal

        // TODO: validate DPoP

        // PKCE: code_verifier, code_challenge, code_challenge_method
        var clientSettings = openIdClient.Settings;
        ValidatePkce(
            tokenRequest.CodeVerifier,
            authorizationRequest.CodeChallenge,
            authorizationRequest.CodeChallengeMethod,
            clientSettings.RequireCodeChallenge);

        return ValueTask.CompletedTask;
    }

    private void ValidatePkce(
        string? codeVerifier,
        string? codeChallenge,
        string? codeChallengeMethod,
        bool requireCodeChallenge)
    {
        var hasCodeChallenge = !string.IsNullOrEmpty(codeChallenge);
        var requireCodeVerifier = hasCodeChallenge || requireCodeChallenge;

        if (!string.IsNullOrEmpty(codeVerifier))
        {
            if (!hasCodeChallenge)
            {
                throw ErrorFactory
                    .InvalidGrant("The 'code_challenge' parameter was missing in the original authorization request.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }

            var expectedCodeChallenge = codeChallengeMethod switch
            {
                OpenIdConstants.CodeChallengeMethods.Plain => codeVerifier,
                OpenIdConstants.CodeChallengeMethods.S256 => CryptoService.HashValue(
                    codeVerifier,
                    HashAlgorithmType.Sha256,
                    BinaryEncodingType.Base64Url,
                    SecureEncoding.ASCII),
                _ => null
            };

            // TODO: provide a way to allow custom code challenge methods

            if (expectedCodeChallenge is null)
            {
                throw ErrorFactory
                    .InvalidGrant("The provided 'code_challenge_method' parameter contains a value that is not supported.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }

            if (!CryptoService.FixedTimeEquals(expectedCodeChallenge.AsSpan(), codeChallenge.AsSpan()))
            {
                throw ErrorFactory
                    .InvalidGrant("PKCE verification has failed.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }
        }
        else if (requireCodeVerifier)
        {
            throw ErrorFactory
                .InvalidGrant("The 'code_verifier' parameter is required.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }
    }

    private async ValueTask<TokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        AuthorizationGrant authorizationGrant,
        CancellationToken cancellationToken)
    {
        var (authorizationRequest, authenticationTicket) = authorizationGrant;

        var scopes = tokenRequest.Scopes;
        Debug.Assert(scopes is not null);

        var tokenResponse = TokenResponse.Create(OpenIdServer);

        tokenResponse.Scopes = scopes;

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds(),
            Nonce = authorizationRequest.Nonce,
            State = authorizationRequest.State,
            Scopes = authorizationRequest.Scopes,
            AuthorizationCode = tokenRequest.Code,
            Subject = authenticationTicket.Subject,
            SubjectId = authenticationTicket.SubjectId,
            AuthenticationProperties = authenticationTicket.AuthenticationProperties
        };

        {
            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken);

            tokenResponse.AccessToken = securityToken.Value;
            tokenResponse.ExpiresIn = securityToken.Lifetime?.Duration;
            tokenResponse.TokenType = OpenIdConstants.TokenTypes.Bearer; // TODO: add support for DPoP
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
        {
            var newRequest = securityTokenRequest with { AccessToken = tokenResponse.AccessToken };

            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken);

            tokenResponse.IdToken = securityToken.Value;
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
        {
            // TODO
            tokenResponse.RefreshToken = "TODO";
        }

        return tokenResponse;
    }
}
