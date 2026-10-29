#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="IIdTokenValidator"/> that first decrypts an encrypted ID token with
/// the client's keys, then verifies its signature with the provider's signing keys, and then checks its claims
/// (OpenID Connect Core §3.1.3.7).
/// </summary>
internal class DefaultIdTokenValidator(
    TimeProvider timeProvider,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IProviderMetadataResolver metadataResolver,
    ProviderJwtValidator providerJwtValidator
) : IIdTokenValidator
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;
    private ProviderJwtValidator ProviderJwtValidator { get; } = providerJwtValidator;

    /// <inheritdoc />
    public async ValueTask<IdTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        IdTokenValidationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.IdToken))
            return Fail(TokenValidationFailureCodes.InvalidToken, "The ID token is empty.");

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var issuer =
            metadata.Issuer
            ?? throw new OpenIdProviderException(
                "The discovery document of the provider lacks the issuer."
            );

        var validation = await ProviderJwtValidator.ValidateAsync(
            request.IdToken,
            issuer,
            ProviderJwtProfile.ForIdToken(registration, metadata),
            cancellationToken
        );
        if (!validation.IsValid)
            return Fail(validation.FailureCode, validation.FailureReason);

        var decodedJwt = validation.DecodedJwt;
        var header = decodedJwt.Header;
        var payload = decodedJwt.Payload;

        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Sub, out var sub)
            || sub.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(sub.GetString())
        )
            return Fail(
                TokenValidationFailureCodes.InvalidClaim,
                "The ID token has no 'sub' claim."
            );

        if (
            request.Subject is { } expectedSubject
            && !string.Equals(sub.GetString(), expectedSubject, StringComparison.Ordinal)
        )
            return Fail(
                TokenValidationFailureCodes.SubjectMismatch,
                "The 'sub' claim of the ID token does not match the expected subject."
            );

        if (
            payload.TryGetProperty(JoseClaimNames.Payload.Azp, out var azp)
            && (
                azp.ValueKind != JsonValueKind.String
                || !string.Equals(azp.GetString(), registration.ClientId, StringComparison.Ordinal)
            )
        )
            return Fail(
                TokenValidationFailureCodes.InvalidAudience,
                "The 'azp' claim of the ID token does not match the client."
            );

        if (
            request.Nonce is { } expectedNonce
            && (
                !payload.TryGetProperty(JoseClaimNames.Payload.Nonce, out var nonce)
                || nonce.ValueKind != JsonValueKind.String
                || !string.Equals(nonce.GetString(), expectedNonce, StringComparison.Ordinal)
            )
        )
            return Fail(
                TokenValidationFailureCodes.NonceMismatch,
                "The 'nonce' claim of the ID token does not match the request."
            );

        if (request.MaxAge is { } maxAge)
        {
            if (
                !payload.TryGetPropertyNumericDate(
                    JoseClaimNames.Payload.AuthTime,
                    out var authTime
                )
            )
                return Fail(
                    TokenValidationFailureCodes.InvalidClaim,
                    "The ID token has no valid 'auth_time' claim."
                );

            if (TimeProvider.GetUtcNow() - authTime - ProviderJwtValidator.ClockSkew > maxAge)
                return Fail(
                    TokenValidationFailureCodes.MaxAgeExceeded,
                    "The authentication of the ID token is older than the maximum age."
                );
        }

        if (CheckHashes(header, payload, decodedJwt.SigningKey, request) is { } hashFailure)
            return hashFailure;

        var identity = await validation.Result.GetClaimsIdentityAsync(cancellationToken);
        return new IdTokenValidationResult(payload) { Identity = identity };
    }

    private IdTokenValidationResult? CheckHashes(
        JsonElement header,
        JsonElement payload,
        SecretKey signingKey,
        IdTokenValidationRequest request
    )
    {
        if (
            string.IsNullOrEmpty(request.AccessToken)
            && string.IsNullOrEmpty(request.AuthorizationCode)
            && string.IsNullOrEmpty(request.State)
        )
            return null;

        if (
            !header.TryGetProperty(JoseClaimNames.Header.Alg, out var algElement)
            || algElement.ValueKind != JsonValueKind.String
            || algElement.GetString() is not { } headerAlg
            || !AlgorithmCollectionProvider.Collection.TryGetSignatureAlgorithm(
                headerAlg,
                out var signatureAlgorithm
            )
        )
            return Fail(
                TokenValidationFailureCodes.AlgorithmNotPermitted,
                "The ID token signing algorithm is not registered."
            );

        var hashFunction = signatureAlgorithm.GetTokenHashFunction(signingKey, out var hashSize);
        Span<byte> hashBuffer = stackalloc byte[hashSize];

        if (
            CheckHash(
                payload,
                JoseClaimNames.Payload.AtHash,
                request.AccessToken,
                hashFunction,
                hashBuffer
            ) is
            { } atHashFailure
        )
            return atHashFailure;

        if (
            CheckHash(
                payload,
                JoseClaimNames.Payload.CHash,
                request.AuthorizationCode,
                hashFunction,
                hashBuffer
            ) is
            { } cHashFailure
        )
            return cHashFailure;

        if (
            CheckHash(
                payload,
                JoseClaimNames.Payload.SHash,
                request.State,
                hashFunction,
                hashBuffer
            ) is
            { } sHashFailure
        )
            return sHashFailure;

        return null;
    }

    private static IdTokenValidationResult? CheckHash(
        JsonElement payload,
        string claimName,
        string? value,
        HashFunctionDelegate hashFunction,
        Span<byte> hashBuffer
    )
    {
        if (string.IsNullOrEmpty(value) || !payload.TryGetProperty(claimName, out var claim))
            return null;

        if (
            claim.ValueKind != JsonValueKind.String
            || claim.GetString() is not { } tokenHash
            || !TokenHash.Verify(hashFunction, value, tokenHash, hashBuffer)
        )
            return Fail(
                TokenValidationFailureCodes.TokenHashMismatch,
                $"The ID token '{claimName}' does not match."
            );

        return null;
    }

    private static IdTokenValidationResult Fail(string failureCode, string reason) =>
        new(failureCode, reason);
}
