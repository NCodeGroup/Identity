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
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Endpoints.Token.Results;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Endpoints.Token.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>Authorization Code</c> grant type.
/// </summary>
public class DefaultAuthorizationCodeGrantHandler(
    OpenIdServer openIdServer,
    IOpenIdErrorFactory errorFactory,
    IPersistedGrantService persistedGrantService,
    ICryptoService cryptoService
) : ITokenGrantHandler, ICommandHandler<ValidateAuthorizationCodeGrantCommand>
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public string GrantType => OpenIdConstants.GrantTypes.AuthorizationCode;

    /// <inheritdoc />
    public async ValueTask<ITokenResponse> HandleAsync(
        TokenRequestContext tokenRequestContext,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest) = tokenRequestContext;
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

        var grantOrNull = await PersistedGrantService.TryGetAsync<IAuthorizationRequest>(
            grantId,
            singleUse: true,
            setConsumed: true,
            cancellationToken);

        if (!grantOrNull.HasValue)
            throw ErrorFactory
                .InvalidGrant("The provided authorization code is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        var grant = grantOrNull.Value;
        var authorizationRequest = grant.Payload;

        var grantContext = new AuthorizationCodeGrantContext(
            openIdContext,
            openIdClient,
            tokenRequest,
            authorizationRequest);

        var validateCommand = new ValidateAuthorizationCodeGrantCommand(grantContext);

        await mediator.SendAsync(
            validateCommand,
            cancellationToken);

        var tokenResponse = await CreateTokenResponseAsync(
            grantContext,
            cancellationToken);

        return tokenResponse;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateAuthorizationCodeGrantCommand command,
        CancellationToken cancellationToken)
    {
        var context = command.GrantContext;
        var (openIdContext, openIdClient, tokenRequest, authorizationRequest) = context;

        var clientId = openIdClient.ClientId;
        var clientSettings = openIdClient.Settings;

        if (authorizationRequest.ClientId != clientId)
        {
            throw ErrorFactory
                .InvalidGrant("The provided authorization code was issued to another client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        var redirectUri = tokenRequest.RedirectUri;
        if (authorizationRequest.RedirectUri != redirectUri)
        {
            throw ErrorFactory
                .InvalidGrant("The provided redirect uri does not match the authorization request.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // PKCE: code_verifier, code_challenge, code_challenge_method
        ValidatePkce(
            tokenRequest.CodeVerifier,
            authorizationRequest.CodeChallenge,
            authorizationRequest.CodeChallengeMethod,
            clientSettings.RequireCodeChallenge);
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

            var transformedCodeVerifier = codeChallengeMethod switch
            {
                OpenIdConstants.CodeChallengeMethods.Plain => codeVerifier,
                OpenIdConstants.CodeChallengeMethods.S256 => CryptoService.HashValue(
                    codeVerifier,
                    HashAlgorithmType.Sha256,
                    BinaryEncodingType.Base64Url,
                    SecureEncoding.ASCII),
                _ => null
            };

            if (transformedCodeVerifier is null)
            {
                throw ErrorFactory
                    .InvalidGrant("The provided 'code_challenge_method' parameter contains a value that is not supported.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }

            if (!CryptoService.FixedTimeEquals(codeChallenge.AsSpan(), transformedCodeVerifier.AsSpan()))
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

    private ValueTask<TokenResponse> CreateTokenResponseAsync(
        AuthorizationCodeGrantContext grantContext,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest, authorizationRequest) = grantContext;

        var scopes = tokenRequest.Scopes;
        Debug.Assert(scopes is not null);

        // TODO: issue token(s)
        var tokenResponse = TokenResponse.Create(OpenIdServer);

        tokenResponse.AccessToken = "TODO";

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
        {
            tokenResponse.IdToken = "TODO";
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
        {
            tokenResponse.RefreshToken = "TODO";
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(tokenResponse);
    }
}
