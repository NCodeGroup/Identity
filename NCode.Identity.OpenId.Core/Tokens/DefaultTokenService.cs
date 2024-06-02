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
using NCode.Identity.OpenId.Extensions;
using NCode.Identity.OpenId.Tokens.Commands;
using NCode.Identity.OpenId.Tokens.Models;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId.Tokens;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenService"/> abstraction.
/// </summary>
public class DefaultTokenService(
    IAlgorithmProvider algorithmProvider,
    ICredentialSelector credentialSelector,
    IJsonWebTokenService jsonWebTokenService
) : ITokenService
{
    private IAlgorithmProvider AlgorithmProvider { get; } = algorithmProvider;
    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateAccessTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken)
    {
        var clientSettings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;
        var openIdTenant = openIdContext.Tenant;
        var secretKeys = openIdTenant.SecretKeyProvider.Collection;

        var signingCredentials = GetSigningCredentials(
            AlgorithmProvider.Collection,
            clientSettings.AccessTokenSigningAlgValuesSupported,
            secretKeys);

        var encryptionCredentials = GetEncryptionCredentials(
            AlgorithmProvider.Collection,
            clientSettings.AccessTokenEncryptionRequired,
            clientSettings.AccessTokenEncryptionAlgValuesSupported,
            clientSettings.AccessTokenEncryptionEncValuesSupported,
            clientSettings.AccessTokenEncryptionZipValuesSupported,
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

        var createdWhen = tokenRequest.CreatedWhen;
        var lifetime = clientSettings.AccessTokenLifetime;
        var tokenPeriod = new TimePeriod(createdWhen, lifetime);

        var parameters = new EncodeJwtParameters
        {
            TokenType = clientSettings.AccessTokenType,

            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,

            IssuedAt = tokenPeriod.StartTime,
            NotBefore = tokenPeriod.StartTime,
            Expires = tokenPeriod.EndTime,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payloadClaims,

            // TODO: can we use mediator for these too?
            ExtraSignatureHeaderClaims = tokenRequest.ExtraSignatureHeaderClaims,
            ExtraEncryptionHeaderClaims = tokenRequest.ExtraEncryptionHeaderClaims
        };

        var jwt = JsonWebTokenService.EncodeJwt(parameters);
        var securityToken = new SecurityToken(tokenContext.TokenType, jwt, tokenPeriod);
        var subjectId = tokenRequest.SubjectId ?? tokenRequest.Subject?.Claims.GetSubjectIdOrDefault();

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
        var clientSettings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;
        var openIdTenant = openIdContext.Tenant;
        var secretKeys = openIdTenant.SecretKeyProvider.Collection;

        var signingCredentials = GetSigningCredentials(
            AlgorithmProvider.Collection,
            clientSettings.IdTokenSigningAlgValuesSupported,
            secretKeys);

        var encryptionCredentials = GetEncryptionCredentials(
            AlgorithmProvider.Collection,
            clientSettings.IdTokenEncryptionRequired,
            clientSettings.IdTokenEncryptionAlgValuesSupported,
            clientSettings.IdTokenEncryptionEncValuesSupported,
            clientSettings.IdTokenEncryptionZipValuesSupported,
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

        var createdWhen = tokenRequest.CreatedWhen;
        var lifetime = clientSettings.IdTokenLifetime;
        var tokenPeriod = new TimePeriod(createdWhen, lifetime);

        var parameters = new EncodeJwtParameters
        {
            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,

            IssuedAt = tokenPeriod.StartTime,
            NotBefore = tokenPeriod.StartTime,
            Expires = tokenPeriod.EndTime,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payloadClaims,

            // TODO: can we use mediator for these too?
            ExtraSignatureHeaderClaims = tokenRequest.ExtraSignatureHeaderClaims,
            ExtraEncryptionHeaderClaims = tokenRequest.ExtraEncryptionHeaderClaims
        };

        var jwt = JsonWebTokenService.EncodeJwt(parameters);
        var securityToken = new SecurityToken(tokenContext.TokenType, jwt, tokenPeriod);
        var subjectId = tokenRequest.SubjectId ?? tokenRequest.Subject?.Claims.GetSubjectIdOrDefault();

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
