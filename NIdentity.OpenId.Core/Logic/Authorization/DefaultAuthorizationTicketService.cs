#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using System.Security.Claims;
using System.Text;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Identity.JsonWebTokens;
using NCode.Jose;
using NCode.Jose.Algorithms;
using NCode.Jose.Credentials;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Results;

namespace NIdentity.OpenId.Logic.Authorization;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationTicketService"/> abstraction.
/// </summary>
public class DefaultAuthorizationTicketService(
    ICryptoService cryptoService,
    IAlgorithmProvider algorithmProvider,
    ICredentialSelector credentialSelector,
    IJsonWebTokenService jsonWebTokenService,
    IAuthorizationClaimsService authorizationClaimsService,
    IPersistedGrantService persistedGrantService
) : IAuthorizationTicketService
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private IAlgorithmProvider AlgorithmProvider { get; } = algorithmProvider;
    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
    private IAuthorizationClaimsService AuthorizationClaimsService { get; } = authorizationClaimsService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public virtual async ValueTask CreateAuthorizationCodeAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var tenantId = authorizationContext.OpenIdContext.OpenIdTenant.TenantId;
        var clientId = authorizationContext.Client.ClientId;
        var subjectId = command.AuthenticationTicket.Principal.FindFirstValue(JoseClaimNames.Payload.Sub);

        var grantKey = CryptoService.GenerateUrlSafeKey();

        await PersistedGrantService.AddAsync(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            grantKey,
            clientId,
            subjectId,
            authorizationContext.ClientSettings.AuthorizationCodeLifetime,
            payload: authorizationContext.AuthorizationRequest,
            cancellationToken);

        ticket.Code = grantKey;
    }

    /// <inheritdoc />
    public virtual async ValueTask CreateAccessTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var openIdContext = authorizationContext.OpenIdContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var clientSettings = authorizationContext.ClientSettings;
        var authenticationTicket = command.AuthenticationTicket;

        var openIdTenant = openIdContext.OpenIdTenant;
        var secretKeys = openIdTenant.SecretKeyProvider.SecretKeys;

        var signingCredentials = GetSigningCredentials(
            AlgorithmProvider.Algorithms,
            clientSettings.AccessTokenSigningAlgValuesSupported,
            secretKeys);

        var encryptionCredentials = GetEncryptionCredentials(
            AlgorithmProvider.Algorithms,
            clientSettings.AccessTokenEncryptionRequired,
            clientSettings.AccessTokenEncryptionAlgValuesSupported,
            clientSettings.AccessTokenEncryptionEncValuesSupported,
            clientSettings.AccessTokenEncryptionZipValuesSupported,
            secretKeys);

        const int byteLength = 16; // aka 128 bits which is larger than the entropy of GUID v4 (122 bits)
        var tokenId = CryptoService.GenerateUrlSafeKey(byteLength);
        var createdWhen = ticket.CreatedWhen;

        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JoseClaimNames.Payload.Jti] = tokenId,
            [JoseClaimNames.Payload.ClientId] = authorizationRequest.ClientId
        };

        // TODO
        // sid
        // cnf
        // [client claims] (Client.AlwaysSendClientClaims, Client.ClientClaimsPrefix)
        // scope (except offline_access unless subject is present)
        // [required subject claims (sub, auth_time, idp, amr)]
        // [optional subject claims (acr)]
        // [api resource claims]
        // [api scope claims]
        // [custom claims]

        var subjectClaims = await AuthorizationClaimsService
            .GetAccessTokenClaimsAsync(
                authorizationContext,
                authenticationTicket,
                createdWhen,
                cancellationToken)
            .ToListAsync(cancellationToken);

        var lifetime = clientSettings.AccessTokenLifetime;

        var parameters = new EncodeJwtParameters
        {
            TokenType = clientSettings.AccessTokenType,

            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = authorizationRequest.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = createdWhen + lifetime,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payload
        };

        var accessToken = JsonWebTokenService.EncodeJwt(parameters);

        // TODO: publish event of new access token

        ticket.AccessToken = accessToken;
        ticket.TokenType = OpenIdConstants.TokenTypes.Bearer;
        ticket.ExpiresIn = lifetime;
    }

    /// <inheritdoc />
    public virtual async ValueTask CreateIdTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var openIdContext = authorizationContext.OpenIdContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var clientSettings = authorizationContext.ClientSettings;
        var authenticationTicket = command.AuthenticationTicket;

        // References:
        // https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        // https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        // https://learn.microsoft.com/en-us/azure/active-directory/develop/id-token-claims-reference

        var openIdTenant = openIdContext.OpenIdTenant;
        var secretKeys = openIdTenant.SecretKeyProvider.SecretKeys;

        var signingCredentials = GetSigningCredentials(
            AlgorithmProvider.Algorithms,
            clientSettings.IdTokenSigningAlgValuesSupported,
            secretKeys);

        var encryptionCredentials = GetEncryptionCredentials(
            AlgorithmProvider.Algorithms,
            clientSettings.IdTokenEncryptionRequired,
            clientSettings.IdTokenEncryptionAlgValuesSupported,
            clientSettings.IdTokenEncryptionEncValuesSupported,
            clientSettings.IdTokenEncryptionZipValuesSupported,
            secretKeys);

        var createdWhen = ticket.CreatedWhen;
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);

        // nonce
        var nonce = authorizationRequest.Nonce;
        if (!string.IsNullOrEmpty(nonce))
        {
            payload[JoseClaimNames.Payload.Nonce] = nonce;
        }

        GenerateParameterHashes(
            ticket,
            payload,
            signingCredentials);

        // TODO: sid (if present from request)

        var subjectClaims = await AuthorizationClaimsService
            .GetIdTokenClaimsAsync(
                authorizationContext,
                authenticationTicket,
                createdWhen,
                cancellationToken)
            .ToListAsync(cancellationToken);

        var parameters = new EncodeJwtParameters
        {
            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = authorizationRequest.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = createdWhen + clientSettings.IdTokenLifetime,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payload
        };

        // TODO: publish event of new id token

        ticket.IdToken = JsonWebTokenService.EncodeJwt(parameters);
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

    private static void GenerateParameterHashes(
        IAuthorizationTicket ticket,
        IDictionary<string, object> payload,
        JoseSigningCredentials signingCredentials)
    {
        var hashAlgorithmName = signingCredentials.SignatureAlgorithm.HashAlgorithmName;
        var hashSizeBits = hashAlgorithmName.GetHashSizeBits();
        var hashFunction = hashAlgorithmName.GetHashFunction();

        var hashSizeBytes = (hashSizeBits + 7) >> 3;
        Span<byte> hashBuffer = stackalloc byte[hashSizeBytes];

        // c_hash
        var authorizationCode = ticket.Code;
        if (!string.IsNullOrEmpty(authorizationCode))
        {
            payload[JoseClaimNames.Payload.CHash] = GetHashValue(
                authorizationCode,
                hashFunction,
                hashBuffer);
        }

        // at_hash
        var accessToken = ticket.AccessToken;
        if (!string.IsNullOrEmpty(accessToken))
        {
            payload[JoseClaimNames.Payload.AtHash] = GetHashValue(
                accessToken,
                hashFunction,
                hashBuffer);
        }

        // s_hash
        var state = ticket.State;
        if (!string.IsNullOrEmpty(state))
        {
            payload[JoseClaimNames.Payload.SHash] = GetHashValue(
                state,
                hashFunction,
                hashBuffer);
        }
    }

    private static string GetHashValue(
        string value,
        HashFunctionDelegate hashFunction,
        Span<byte> hashBuffer)
    {
        /*
            Specification from `c_hash` but also applies to `at_hash` and `s_hash`:

            Code hash value. Its value is the base64url encoding of the left-most half of the hash of the octets of the ASCII
            representation of the code value, where the hash algorithm used is the hash algorithm used in the alg Header
            Parameter of the ID Token's JOSE Header. For instance, if the alg is HS512, hash the code value with SHA-512, then
            take the left-most 256 bits and base64url encode them. The c_hash value is a case sensitive string.
        */

        var encodeByteCount = Encoding.ASCII.GetByteCount(value);
        using var encodeLease = CryptoPool.Rent(encodeByteCount, isSensitive: false, out Span<byte> encodeBuffer);
        var encodeBytesWritten = Encoding.ASCII.GetBytes(value, encodeBuffer);
        Debug.Assert(encodeBytesWritten == encodeByteCount);

        var hashResult = hashFunction(encodeBuffer, hashBuffer, out var hashBytesWritten);
        Debug.Assert(hashResult && hashBytesWritten == hashBuffer.Length);

        var halfCount = hashBytesWritten / 2;
        return Base64Url.Encode(hashBuffer[..halfCount]);
    }
}
