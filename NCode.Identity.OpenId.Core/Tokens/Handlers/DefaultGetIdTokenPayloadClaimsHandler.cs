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
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Tokens.Commands;

namespace NCode.Identity.OpenId.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetIdTokenPayloadClaimsCommand"/> handler that generates the payload
/// claims for an id token. This handler is responsible for generating protocol claims.
/// </summary>
public class DefaultGetIdTokenPayloadClaimsHandler : ICommandHandler<GetIdTokenPayloadClaimsCommand>, ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriority;

    /// <inheritdoc />
    public ValueTask HandleAsync(GetIdTokenPayloadClaimsCommand command, CancellationToken cancellationToken)
    {
        var (openIdContext, _, tokenContext, payloadClaims) = command;
        var (tokenRequest, signingCredentials, _, _) = tokenContext;

        // tid
        var tenant = openIdContext.Tenant;
        payloadClaims[JoseClaimNames.Payload.Tid] = tenant.TenantId;

        // nonce
        var nonce = tokenRequest.Nonce;
        if (!string.IsNullOrEmpty(nonce))
        {
            payloadClaims[JoseClaimNames.Payload.Nonce] = nonce;
        }

        // c_hash, at_hash, s_hash
        GenerateParameterHashes(
            tokenRequest.AuthorizationCode,
            tokenRequest.AccessToken,
            tokenRequest.State,
            payloadClaims,
            signingCredentials
        );

        // TODO: client_id, acr, sid (if present from request), cnf
        // TODO: azp (https://bitbucket.org/openid/connect/issues/973/)

        return ValueTask.CompletedTask;
    }

    private static void GenerateParameterHashes(
        string? authorizationCode,
        string? accessToken,
        string? state,
        IDictionary<string, object> payloadClaims,
        JoseSigningCredentials signingCredentials)
    {
        var hashAlgorithmName = signingCredentials.SignatureAlgorithm.HashAlgorithmName;
        var hashSizeBits = hashAlgorithmName.GetHashSizeBits();
        var hashFunction = hashAlgorithmName.GetHashFunction();

        var hashSizeBytes = (hashSizeBits + 7) >> 3;
        Span<byte> hashBuffer = stackalloc byte[hashSizeBytes];

        // c_hash
        if (!string.IsNullOrEmpty(authorizationCode))
        {
            payloadClaims[JoseClaimNames.Payload.CHash] = GetHashValue(
                authorizationCode,
                hashFunction,
                hashBuffer);
        }

        // at_hash
        if (!string.IsNullOrEmpty(accessToken))
        {
            payloadClaims[JoseClaimNames.Payload.AtHash] = GetHashValue(
                accessToken,
                hashFunction,
                hashBuffer);
        }

        // s_hash
        if (!string.IsNullOrEmpty(state))
        {
            payloadClaims[JoseClaimNames.Payload.SHash] = GetHashValue(
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
            Specification from `c_hash` but also applies to `at_hash` and `s_hash`...

            Code hash value. Its value is the base64url encoding of the left-most half of the hash of the octets of the ASCII
            representation of the code value, where the hash algorithm used is the hash algorithm used in the alg Header
            Parameter of the ID Token's JOSE Header. For instance, if the alg is HS512, hash the code value with SHA-512, then
            take the left-most 256 bits and base64url encode them. The c_hash value is a case-sensitive string.
        */

        var encoding = SecureEncoding.ASCII;
        var encodeByteCount = encoding.GetByteCount(value);
        using var _ = CryptoPool.Rent(encodeByteCount, isSensitive: false, out Span<byte> encodeBuffer);
        var encodeBytesWritten = encoding.GetBytes(value, encodeBuffer);
        Debug.Assert(encodeBytesWritten == encodeByteCount);

        var hashResult = hashFunction(encodeBuffer, hashBuffer, out var hashBytesWritten);
        Debug.Assert(hashResult && hashBytesWritten == hashBuffer.Length);

        var halfCount = hashBytesWritten / 2;
        return Base64Url.Encode(hashBuffer[..halfCount]);
    }
}
