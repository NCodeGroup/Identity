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
using NCode.Jose.Extensions;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

public interface IUrlHelper
{
    string EncodeCanonicalUrl(UriDescriptor uriDescriptor);

    string EncodeAbsoluteUri(UriDescriptor uriDescriptor);
}

internal class CreateAuthorizationTicketHandler : ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private ISystemClock SystemClock { get; }
    private IIdGenerator<long> IdGenerator { get; }
    private IOpenIdMessageContext OpenIdMessageContext { get; }
    private ICryptoService CryptoService { get; }
    private IAuthorizationCodeStore AuthorizationCodeStore { get; }
    private IJoseSerializer JoseSerializer { get; }
    private ICredentialSelector CredentialSelector { get; }
    private IJsonWebTokenService JsonWebTokenService { get; }

    public CreateAuthorizationTicketHandler(
        ISystemClock systemClock,
        IIdGenerator<long> idGenerator,
        IOpenIdMessageContext openIdMessageContext,
        ICryptoService cryptoService,
        IAuthorizationCodeStore authorizationCodeStore,
        IJoseSerializer joseSerializer,
        ICredentialSelector credentialSelector,
        IJsonWebTokenService jsonWebTokenService)
    {
        SystemClock = systemClock;
        IdGenerator = idGenerator;
        OpenIdMessageContext = openIdMessageContext;
        CryptoService = cryptoService;
        AuthorizationCodeStore = authorizationCodeStore;
        JoseSerializer = joseSerializer;
        CredentialSelector = credentialSelector;
        JsonWebTokenService = jsonWebTokenService;
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

        var ticket = new AuthorizationTicket(OpenIdMessageContext)
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

        ticket.Issuer = endpointContext.Tenant.Issuer;

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
        var authorizationRequestJson = JsonSerializer.Serialize(authorizationRequest, OpenIdMessageContext.JsonSerializerOptions);

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
        var utcNow = SystemClock.UtcNow;

        var endpointContext = command.EndpointContext;
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;

        var authenticateResult = command.AuthenticateResult;
        Debug.Assert(authenticateResult.Succeeded);

        // References:
        // https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        // https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        // https://learn.microsoft.com/en-us/azure/active-directory/develop/id-token-claims-reference

        var tokenConfiguration = authorizationContext.Client.IdTokenConfiguration;

        if (!CredentialSelector.TryGetSigningCredentials(
                tokenConfiguration.SignatureAlgorithms,
                out var signingCredentials))
            throw new InvalidOperationException("TODO: no signing credentials found.");

        JoseEncryptingCredentials? encryptingCredentials = null;
        var requireEncryption = tokenConfiguration.RequireEncryption;
        if (requireEncryption &&
            !CredentialSelector.TryGetEncryptingCredentials(
                tokenConfiguration.KeyManagementAlgorithms,
                tokenConfiguration.EncryptionAlgorithms,
                tokenConfiguration.CompressionAlgorithms,
                out encryptingCredentials))
            throw new InvalidOperationException("TODO: no encrypting credentials found.");

        var hashAlgorithmName = signingCredentials.SignatureAlgorithm.HashAlgorithmName;
        var hashSizeBits = hashAlgorithmName.GetHashSizeBits();
        var hashFunction = hashAlgorithmName.GetHashFunction();

        var authTime = authenticateResult.Properties.IssuedUtc ?? utcNow;
        var payload = new Dictionary<string, object>
        {
            [JoseClaimNames.Payload.AuthTime] = authTime.ToUnixTimeSeconds()
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

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Sub
        };

        // add standard claims requested by the client via the scope parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims

        // TODO: use constants

        if (authorizationRequest.Scopes.Contains("profile"))
        {
            claimTypes.UnionWith(new[]
            {
                "name",
                "family_name",
                "given_name",
                "middle_name",
                "nickname",
                "preferred_username",
                "profile",
                "picture",
                "website",
                "gender",
                "birthdate",
                "zoneinfo",
                "locale",
                "updated_at"
            });
        }

        if (authorizationRequest.Scopes.Contains("email"))
        {
            claimTypes.UnionWith(new[]
            {
                "email",
                "email_verified"
            });
        }

        if (authorizationRequest.Scopes.Contains("address"))
        {
            claimTypes.UnionWith(new[]
            {
                "address"
            });
        }

        if (authorizationRequest.Scopes.Contains("phone"))
        {
            claimTypes.UnionWith(new[]
            {
                "phone_number",
                "phone_number_verified"
            });
        }

        // TODO: add specific claims requested by the client via the claims parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter

        // TODO: should we also include profile claims based on a client configuration setting?

        // TODO: other claims...
        // client_id
        // acr
        // amr
        // azp (https://bitbucket.org/openid/connect/issues/973/)
        // sid
        // cnf

        var subject = authenticateResult.Principal.Identities.First();
        var filteredClaims = subject.Claims.Where(claim => claimTypes.Contains(claim.Type));

        var parameters = new EncodeJwtParameters
        {
            SigningOptions = new JoseSigningOptions(signingCredentials),
            EncryptingOptions = encryptingCredentials is not null ?
                new JoseEncryptingOptions(encryptingCredentials) :
                null,

            Issuer = endpointContext.Tenant.Issuer,
            Audience = authorizationRequest.ClientId,

            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = utcNow + tokenConfiguration.Lifetime,

            SubjectClaims = filteredClaims,
            ExtraPayloadClaims = payload
        };

        return JsonWebTokenService.EncodeJwt(parameters);
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
