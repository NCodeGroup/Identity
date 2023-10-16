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
using System.Text;
using System.Text.Json;
using IdGen;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Identity.JsonWebTokens;
using NCode.Jose;
using NCode.Jose.Algorithms;
using NCode.Jose.Credentials;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Logic.Authorization;

/// <summary>
/// Provides a default implementation for the <see cref="IAuthorizationTicketService"/> abstraction.
/// </summary>
public class AuthorizationTicketService : IAuthorizationTicketService
{
    private IIdGenerator<long> IdGenerator { get; }
    private ICryptoService CryptoService { get; }
    private IAlgorithmProvider AlgorithmProvider { get; }
    private ICredentialSelector CredentialSelector { get; }
    private IJsonWebTokenService JsonWebTokenService { get; }
    private IAuthorizationClaimsService AuthorizationClaimsService { get; }
    private IAuthorizationCodeStore AuthorizationCodeStore { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTicketService"/> class.
    /// </summary>
    public AuthorizationTicketService(
        IIdGenerator<long> idGenerator,
        ICryptoService cryptoService,
        IAlgorithmProvider algorithmProvider,
        ICredentialSelector credentialSelector,
        IJsonWebTokenService jsonWebTokenService,
        IAuthorizationClaimsService authorizationClaimsService,
        IAuthorizationCodeStore authorizationCodeStore)
    {
        IdGenerator = idGenerator;
        CryptoService = cryptoService;
        AlgorithmProvider = algorithmProvider;
        CredentialSelector = credentialSelector;
        JsonWebTokenService = jsonWebTokenService;
        AuthorizationClaimsService = authorizationClaimsService;
        AuthorizationCodeStore = authorizationCodeStore;
    }

    /// <inheritdoc />
    public virtual async ValueTask CreateAuthorizationCodeAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var context = command.AuthorizationContext;

        var createdWhen = ticket.CreatedWhen;
        var expiresWhen = createdWhen + context.Client.AuthorizationCodeLifetime;

        var (code, hashedCode) = GenerateAuthorizationCode();
        Debug.Assert(hashedCode.Length <= DataConstants.MaxIndexLength);

        await SaveAuthorizationRequestAsync(
            hashedCode,
            createdWhen,
            expiresWhen,
            context.AuthorizationRequest,
            cancellationToken);

        ticket.Code = code;
    }

    /// <summary>
    /// Generates a new token identifier (aka <c>jti</c>).
    /// The default implementation generates a strong cryptographic random 128-bit value that is Base64Url encoded.
    /// </summary>
    /// <returns>The newly generated token identifier.</returns>
    protected virtual string GenerateTokenId()
    {
        const int byteLength = 16; // aka 128 bits which is larger than the entropy of GUID v4 (122 bits)
        return CryptoService.GenerateKey(byteLength, BinaryEncodingType.Base64Url);
    }

    /// <summary>
    /// Generates a new authorization code and its hashed value.
    /// The default implementation generates a strong cryptographic random 256-bit value that is Base64Url encoded,
    /// the hashed value uses the SHA-256 algorithm and is Base64 encoded.
    /// </summary>
    /// <returns>The newly generated authorization code and its hashed value.</returns>
    protected virtual (string code, string hashedCode) GenerateAuthorizationCode()
    {
        const int byteLength = 32; // aka 256 bits
        var code = CryptoService.GenerateKey(byteLength, BinaryEncodingType.Base64Url);
        var hashedCode = CryptoService.HashValue(code, HashAlgorithmType.Sha256, BinaryEncodingType.Base64);
        return (code, hashedCode);
    }

    /// <summary>
    /// Persists the authorization request to the <see cref="IAuthorizationCodeStore"/>
    /// by using the hashed authorization code as the key.
    /// </summary>
    /// <param name="hashedCode">The hashed authorization code.</param>
    /// <param name="createdWhen">The <see cref="DateTimeOffset"/> when the authorization ticket was created.</param>
    /// <param name="expiresWhen">The <see cref="DateTimeOffset"/> when the authorization ticker expires.</param>
    /// <param name="authorizationRequest">The <see cref="IAuthorizationRequest"/> to persist in the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    protected virtual async ValueTask SaveAuthorizationRequestAsync(
        string hashedCode,
        DateTimeOffset createdWhen,
        DateTimeOffset expiresWhen,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken)
    {
        var jsonSerializerOptions = authorizationRequest.OpenIdContext.JsonSerializerOptions;

        var authorizationRequestJson = JsonSerializer.Serialize(
            authorizationRequest,
            jsonSerializerOptions);

        var id = IdGenerator.CreateId();
        var authorizationCode = new AuthorizationCode
        {
            Id = id,
            HashedCode = hashedCode,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            AuthorizationRequestJson = authorizationRequestJson
        };

        await AuthorizationCodeStore.AddAsync(authorizationCode, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async ValueTask CreateAccessTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var createdWhen = ticket.CreatedWhen;
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationRequest.OpenIdContext;
        var authenticationTicket = command.AuthenticationTicket;

        var client = authorizationContext.Client;
        var tokenConfiguration = client.AccessTokenConfiguration;

        var tenant = openIdContext.Tenant;
        var secretKeys = tenant.SecretKeyProvider.SecretKeys;

        var signingCredentials = GetSigningCredentials(
            AlgorithmProvider.Algorithms,
            tokenConfiguration,
            secretKeys);

        var encryptingCredentials = GetEncryptingCredentials(
            AlgorithmProvider.Algorithms,
            tokenConfiguration,
            secretKeys);

        var tokenId = GenerateTokenId();

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
                cancellationToken)
            .ToListAsync(cancellationToken);

        var parameters = new EncodeJwtParameters
        {
            SigningCredentials = signingCredentials,
            EncryptingCredentials = encryptingCredentials,

            Issuer = tenant.Issuer,
            Audience = authorizationRequest.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = createdWhen + tokenConfiguration.Lifetime,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payload
        };

        var accessToken = JsonWebTokenService.EncodeJwt(parameters);

        // TODO: publish event of new access token

        ticket.AccessToken = accessToken;
        ticket.TokenType = OpenIdConstants.TokenTypes.Bearer;
        ticket.ExpiresIn = tokenConfiguration.Lifetime;
    }

    /// <inheritdoc />
    public virtual async ValueTask CreateIdTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var createdWhen = ticket.CreatedWhen;
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationRequest.OpenIdContext;
        var authenticationTicket = command.AuthenticationTicket;

        // References:
        // https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        // https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        // https://learn.microsoft.com/en-us/azure/active-directory/develop/id-token-claims-reference

        var client = authorizationContext.Client;
        var tokenConfiguration = client.IdTokenConfiguration;

        var tenant = openIdContext.Tenant;
        var secretKeys = tenant.SecretKeyProvider.SecretKeys;

        var signingCredentials = GetSigningCredentials(
            AlgorithmProvider.Algorithms,
            tokenConfiguration,
            secretKeys);

        var encryptingCredentials = GetEncryptingCredentials(
            AlgorithmProvider.Algorithms,
            tokenConfiguration,
            secretKeys);

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
                cancellationToken)
            .ToListAsync(cancellationToken);

        var parameters = new EncodeJwtParameters
        {
            SigningCredentials = signingCredentials,
            EncryptingCredentials = encryptingCredentials,

            Issuer = tenant.Issuer,
            Audience = authorizationRequest.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = createdWhen + tokenConfiguration.Lifetime,

            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = payload
        };

        // TODO: publish event of new id token

        ticket.IdToken = JsonWebTokenService.EncodeJwt(parameters);
    }

    private JoseSigningCredentials GetSigningCredentials(
        IAlgorithmCollection candidateAlgorithms,
        TokenConfiguration tokenConfiguration,
        ISecretKeyCollection secretKeys)
    {
        if (!CredentialSelector.TryGetSigningCredentials(
                candidateAlgorithms,
                tokenConfiguration.SignatureAlgorithms,
                secretKeys,
                out var signingCredentials))
            throw new JoseCredentialsNotFoundException("Unable to locate signing credentials.");
        return signingCredentials;
    }

    private JoseEncryptingCredentials? GetEncryptingCredentials(
        IAlgorithmCollection candidateAlgorithms,
        TokenConfiguration tokenConfiguration,
        ISecretKeyCollection secretKeys)
    {
        JoseEncryptingCredentials? encryptingCredentials = null;
        var requireEncryption = tokenConfiguration.RequireEncryption;
        if (requireEncryption &&
            !CredentialSelector.TryGetEncryptingCredentials(
                candidateAlgorithms,
                tokenConfiguration.KeyManagementAlgorithms,
                tokenConfiguration.EncryptionAlgorithms,
                tokenConfiguration.CompressionAlgorithms,
                secretKeys,
                out encryptingCredentials))
            throw new JoseCredentialsNotFoundException("Unable to locate encrypting credentials.");
        return encryptingCredentials;
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
