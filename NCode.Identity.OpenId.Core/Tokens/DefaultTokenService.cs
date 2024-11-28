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

using System.Security.Claims;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Tokens.Commands;
using NCode.Identity.OpenId.Tokens.Models;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId.Tokens;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenService"/> abstraction.
/// </summary>
public class DefaultTokenService(
    ICryptoService cryptoService,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ICredentialSelector credentialSelector,
    IJsonWebTokenService jsonWebTokenService,
    IPersistedGrantService persistedGrantService
) : ITokenService
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } = algorithmCollectionProvider;
    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateAccessTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var settings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;
        var openIdTenant = openIdContext.Tenant;
        var secretKeys = openIdTenant.SecretsProvider.Collection;

        var signingCredentials = GetSigningCredentials(
            AlgorithmCollectionProvider.Collection,
            settings.AccessTokenSigningAlgValuesSupported,
            secretKeys);

        var encryptionCredentials = GetEncryptionCredentials(
            AlgorithmCollectionProvider.Collection,
            settings.AccessTokenEncryptionRequired,
            settings.AccessTokenEncryptionAlgValuesSupported,
            settings.AccessTokenEncryptionEncValuesSupported,
            settings.AccessTokenEncryptionZipValuesSupported,
            secretKeys);

        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            signingCredentials,
            encryptionCredentials,
            OpenIdConstants.SecurityTokenTypes.AccessToken);

        var subjectClaims = new List<Claim>();
        var payloadClaims = CreatePayloadClaims(tokenRequest.ExtraPayloadClaims);

        await mediator.SendAsync(
            new GetAccessTokenSubjectClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                subjectClaims),
            cancellationToken);

        await mediator.SendAsync(
            new GetAccessTokenPayloadClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                payloadClaims),
            cancellationToken);

        var lifetime = settings.AccessTokenLifetime;
        var createdWhen = tokenRequest.CreatedWhen;
        var expiresWhen = createdWhen + lifetime;
        var tokenPeriod = new TimePeriod(createdWhen, expiresWhen);

        var parameters = new EncodeJwtParameters
        {
            TokenType = settings.AccessTokenType,

            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = expiresWhen,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payloadClaims,

            // TODO: can we use mediator for these too?
            ExtraSignatureHeaderClaims = tokenRequest.ExtraSignatureHeaderClaims,
            ExtraEncryptionHeaderClaims = tokenRequest.ExtraEncryptionHeaderClaims
        };

        var jwt = JsonWebTokenService.EncodeJwt(parameters);
        var securityToken = new SecurityToken(tokenContext.TokenType, jwt, tokenPeriod);
        var subjectId = tokenRequest.SubjectAuthentication?.SubjectId;

        await mediator.SendAsync(
            new SecurityTokenIssuedEvent(openIdContext, openIdClient, subjectId, securityToken),
            cancellationToken);

        return securityToken;
    }

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateIdTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var settings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;
        var openIdTenant = openIdContext.Tenant;
        var secretKeys = openIdTenant.SecretsProvider.Collection;

        var signingCredentials = GetSigningCredentials(
            AlgorithmCollectionProvider.Collection,
            settings.IdTokenSigningAlgValuesSupported,
            secretKeys);

        var encryptionCredentials = GetEncryptionCredentials(
            AlgorithmCollectionProvider.Collection,
            settings.IdTokenEncryptionRequired,
            settings.IdTokenEncryptionAlgValuesSupported,
            settings.IdTokenEncryptionEncValuesSupported,
            settings.IdTokenEncryptionZipValuesSupported,
            secretKeys);

        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            signingCredentials,
            encryptionCredentials,
            OpenIdConstants.SecurityTokenTypes.IdToken);

        var subjectClaims = new List<Claim>();
        var payloadClaims = CreatePayloadClaims(tokenRequest.ExtraPayloadClaims);

        await mediator.SendAsync(
            new GetIdTokenSubjectClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                subjectClaims),
            cancellationToken);

        await mediator.SendAsync(
            new GetIdTokenPayloadClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                payloadClaims),
            cancellationToken);

        var lifetime = settings.IdTokenLifetime;
        var createdWhen = tokenRequest.CreatedWhen;
        var expiresWhen = createdWhen + lifetime;
        var tokenPeriod = new TimePeriod(createdWhen, expiresWhen);

        var parameters = new EncodeJwtParameters
        {
            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = expiresWhen,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payloadClaims,

            // TODO: can we use mediator for these too?
            ExtraSignatureHeaderClaims = tokenRequest.ExtraSignatureHeaderClaims,
            ExtraEncryptionHeaderClaims = tokenRequest.ExtraEncryptionHeaderClaims
        };

        var jwt = JsonWebTokenService.EncodeJwt(parameters);
        var securityToken = new SecurityToken(tokenContext.TokenType, jwt, tokenPeriod);
        var subjectId = tokenRequest.SubjectAuthentication?.SubjectId;

        await mediator.SendAsync(
            new SecurityTokenIssuedEvent(openIdContext, openIdClient, subjectId, securityToken),
            cancellationToken);

        return securityToken;
    }

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var settings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;

        var subjectAuthentication = tokenRequest.SubjectAuthentication;

        var tenantId = openIdContext.Tenant.TenantId;
        var clientId = openIdClient.ClientId;
        var subjectId = subjectAuthentication?.SubjectId;

        var refreshToken = CryptoService.GenerateUrlSafeKey();

        var persistedGrantId = new PersistedGrantId
        {
            TenantId = tenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
            GrantKey = refreshToken
        };

        var refreshTokenGrant = new RefreshTokenGrant(
            clientId,
            tokenRequest.OriginalScopes,
            tokenRequest.EffectiveScopes,
            subjectAuthentication);

        var persistedGrant = new PersistedGrant<RefreshTokenGrant>
        {
            ClientId = clientId,
            SubjectId = subjectId,
            Payload = refreshTokenGrant
        };

        var createdWhen = tokenRequest.CreatedWhen;
        var expirationPolicy = settings.RefreshTokenExpirationPolicy;
        var lifetime = expirationPolicy != OpenIdConstants.RefreshTokenExpirationPolicy.None ?
            settings.RefreshTokenLifetime :
            (TimeSpan?)null;

        await PersistedGrantService.AddAsync(
            persistedGrantId,
            persistedGrant,
            createdWhen,
            lifetime,
            cancellationToken);

        var tokenPeriod = new TimePeriod(createdWhen, createdWhen + lifetime);
        var securityToken = new SecurityToken(
            OpenIdConstants.SecurityTokenTypes.RefreshToken,
            refreshToken,
            tokenPeriod);

        await mediator.SendAsync(
            new SecurityTokenIssuedEvent(openIdContext, openIdClient, subjectId, securityToken),
            cancellationToken);

        return securityToken;
    }

    private static Dictionary<string, object> CreatePayloadClaims(IReadOnlyDictionary<string, object>? initial)
    {
        return initial == null ?
            new Dictionary<string, object>(StringComparer.Ordinal) :
            new Dictionary<string, object>(initial, StringComparer.Ordinal);
    }

    private JoseSigningCredentials GetSigningCredentials(
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> signingAlgValuesSupported,
        ISecretKeyCollection secretKeys)
    {
        if (!CredentialSelector.TryGetSigningCredentials(
                candidateAlgorithms,
                signingAlgValuesSupported,
                secretKeys,
                out var signingCredentials))
        {
            throw new JoseCredentialsNotFoundException("Unable to locate signing credentials.");
        }

        return signingCredentials;
    }

    private JoseEncryptionCredentials? GetEncryptionCredentials(
        IAlgorithmCollection candidateAlgorithms,
        bool encryptionRequired,
        IEnumerable<string> encryptionAlgValuesSupported,
        IEnumerable<string> encryptionEncValuesSupported,
        IEnumerable<string> encryptionZipValuesSupported,
        ISecretKeyCollection secretKeys)
    {
        if (!encryptionRequired)
            return null;

        if (!CredentialSelector.TryGetEncryptionCredentials(
                candidateAlgorithms,
                encryptionAlgValuesSupported,
                encryptionEncValuesSupported,
                encryptionZipValuesSupported,
                secretKeys,
                out var encryptionCredentials))
        {
            throw new JoseCredentialsNotFoundException("Unable to locate encryption credentials.");
        }

        return encryptionCredentials;
    }
}
