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
using NCode.Jose;
using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Models;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class CreateAuthorizationTicketHandler : ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private ISystemClock SystemClock { get; }
    private IIdGenerator<long> IdGenerator { get; }
    private IOpenIdContext OpenIdContext { get; }
    private ICryptoService CryptoService { get; }
    private IAuthorizationCodeStore AuthorizationCodeStore { get; }
    private IJoseSerializer JoseSerializer { get; }
    private IAlgorithmProvider AlgorithmProvider { get; }

    public CreateAuthorizationTicketHandler(
        ISystemClock systemClock,
        IIdGenerator<long> idGenerator,
        IOpenIdContext openIdContext,
        ICryptoService cryptoService,
        IAuthorizationCodeStore authorizationCodeStore,
        IJoseSerializer joseSerializer,
        IAlgorithmProvider algorithmProvider)
    {
        SystemClock = systemClock;
        IdGenerator = idGenerator;
        OpenIdContext = openIdContext;
        CryptoService = cryptoService;
        AuthorizationCodeStore = authorizationCodeStore;
        JoseSerializer = joseSerializer;
        AlgorithmProvider = algorithmProvider;
    }

    // TODO: move back into the original handler and use mediator for each response type

    public async ValueTask<IAuthorizationTicket> HandleAsync(
        CreateAuthorizationTicketCommand command,
        CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;
        var authorizationContext = command.AuthorizationContext;
        var authenticateResult = command.AuthenticateResult;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var responseType = authorizationRequest.ResponseType;

        var ticket = new AuthorizationTicket(OpenIdContext)
        {
            State = authorizationRequest.State
        };

        if (responseType.HasFlag(ResponseTypes.Code))
        {
            ticket.Code = await CreateAuthorizationCodeAsync(authorizationContext, cancellationToken);
        }

        if (responseType.HasFlag(ResponseTypes.Token))
        {
            // TODO
            ticket.TokenType = "Bearer"; // TODO: use constant
            ticket.ExpiresIn = TimeSpan.Zero; // from 'exp' claim
            ticket.AccessToken = "TODO";
            throw new NotImplementedException();
        }

        if (responseType.HasFlag(ResponseTypes.IdToken))
        {
            ticket.IdToken = await CreateIdTokenAsync(
                command,
                ticket,
                cancellationToken);
        }

        ticket.Issuer = "TODO";
        ticket.Issuer = endpointContext.HttpContext.Request.Host.Value;

        return ticket;
    }

    private async ValueTask<string> CreateAuthorizationCodeAsync(
        AuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        const int byteLength = 32;
        var code = CryptoService.GenerateKey(byteLength, BinaryEncodingType.Base64Url);
        var hashedCode = CryptoService.HashValue(code, HashAlgorithmType.Sha256, BinaryEncodingType.Base64);
        Debug.Assert(hashedCode.Length <= DataConstants.MaxIndexLength);

        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var authorizationRequestJson = JsonSerializer.Serialize(authorizationRequest, OpenIdContext.JsonSerializerOptions);

        var client = authorizationContext.Client;
        var lifetime = client.AuthorizationCodeLifetime;
        var createdWhen = SystemClock.UtcNow;
        var expiresWhen = createdWhen + lifetime;

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

        return code;
    }

    private async ValueTask<string> CreateIdTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;

        var authenticateResult = command.AuthenticateResult;
        Debug.Assert(authenticateResult.Succeeded);

        var normalizedAuthenticationClaims =
            authenticateResult.Properties.GetParameter<NormalizedAuthenticationClaims>(NormalizedAuthenticationClaims.Key) ??
            // TODO: moved into extension method, maybe?
            throw new InvalidOperationException();

        // TODO: async?
        var signingParameters = GetIdTokenSigningParameters(authorizationContext.Client.IdTokenSigningAlgorithms);

        var hashAlgorithmName = signingParameters.SignatureAlgorithm.HashAlgorithmName;
        var hashSizeBits = hashAlgorithmName.GetHashSizeBits();
        var hashFunction = hashAlgorithmName.GetHashFunction();

        var issuedWhen = SystemClock.UtcNow;
        var expiresWhen = issuedWhen + authorizationContext.Client.IdTokenLifetime;

        var issuedWhenUnixSeconds = issuedWhen.ToUnixTimeSeconds();
        var expiresWhenUnixSeconds = expiresWhen.ToUnixTimeSeconds();

        // References:
        // https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        // https://learn.microsoft.com/en-us/azure/active-directory/develop/id-token-claims-reference

        var payload = new Dictionary<string, object>
        {
            [JoseClaimNames.Payload.Iss] = "TODO",
            [JoseClaimNames.Payload.Idp] = normalizedAuthenticationClaims.Issuer,
            [JoseClaimNames.Payload.Sub] = normalizedAuthenticationClaims.Subject,
            [JoseClaimNames.Payload.Aud] = authorizationRequest.ClientId,
            [JoseClaimNames.Payload.Nbf] = issuedWhenUnixSeconds,
            [JoseClaimNames.Payload.Iat] = issuedWhenUnixSeconds,
            [JoseClaimNames.Payload.Exp] = expiresWhenUnixSeconds,
            [JoseClaimNames.Payload.AuthTime] = normalizedAuthenticationClaims.AuthTime.ToUnixTimeSeconds()
        };

        // nonce
        var nonce = authorizationRequest.Nonce;
        if (!string.IsNullOrEmpty(nonce))
        {
            payload[JoseClaimNames.Payload.Nonce] = nonce;
        }

        // c_hash
        var authorizationCode = ticket.Code;
        if (!string.IsNullOrEmpty(authorizationCode))
        {
            payload[JoseClaimNames.Payload.CHash] = GetHashValue(
                authorizationCode,
                hashSizeBits,
                hashFunction);
        }

        // at_hash
        var accessToken = ticket.AccessToken;
        if (!string.IsNullOrEmpty(accessToken))
        {
            payload[JoseClaimNames.Payload.AtHash] = GetHashValue(
                accessToken,
                hashSizeBits,
                hashFunction);
        }

        // s_hash
        var state = ticket.State;
        if (!string.IsNullOrEmpty(state))
        {
            payload[JoseClaimNames.Payload.SHash] = GetHashValue(
                state,
                hashSizeBits,
                hashFunction);
        }

        // TODO: other claims...
        // client_id
        // amr
        // acr
        // sid
        // cnf

        // Include profile claims when an access token isn't requested because the
        // client won't be able to access the userinfo endpoint without an access token.
        var includeProfileClaims = !authorizationRequest.ResponseType.HasFlag(ResponseTypes.Token);

        // TODO: add support for standard claims requested by the client (via the scope parameter)
        // https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims

        // TODO: add support for specific claims requested by the client (via the claims parameter)
        // https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter

        // TODO: should we also include profile claims based on a client configuration setting?

        if (includeProfileClaims)
        {
            // TODO: user/profile claims...
        }

        // TODO: header claims...
        // typ

        var extraHeaders = Enumerable.Empty<KeyValuePair<string, object>>();

        var idToken = JoseSerializer.EncodeJws(
            payload,
            signingParameters,
            extraHeaders);

        await ValueTask.CompletedTask;
        throw new NotImplementedException();
    }

    private JoseSigningParameters GetIdTokenSigningParameters(IEnumerable<string> allowedAlgorithmCodes)
    {
        throw new NotImplementedException();
    }

    private static string GetHashValue(string value, int hashSizeBits, HashFunctionDelegate hashFunction)
    {
        var hashSizeBytes = (hashSizeBits + 7) >> 3;

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

        // TODO: Promote this buffer to the calling function since we allocate it three times
        Span<byte> hashBuffer = stackalloc byte[hashSizeBytes];
        var hashResult = hashFunction(encodeBuffer, hashBuffer, out var hashBytesWritten);
        Debug.Assert(hashResult && hashBytesWritten == hashSizeBytes);

        var halfCount = hashSizeBytes / 2;
        return Base64Url.Encode(hashBuffer[..halfCount]);
    }
}
