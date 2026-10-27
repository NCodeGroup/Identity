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

using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="IIdTokenValidator"/> that verifies the signature of an ID token with
/// the provider's signing keys and then checks its claims (OpenID Connect Core §3.1.3.7).
/// </summary>
internal class DefaultIdTokenValidator(
    TimeProvider timeProvider,
    IOptions<OpenIdClientOptions> options,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IProviderMetadataResolver metadataResolver,
    IProviderKeySetResolver keySetResolver,
    IJwtValidator jwtValidator
) : IIdTokenValidator
{
    private const string NoneAlgorithm = "none";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private TimeProvider TimeProvider { get; } = timeProvider;
    private OpenIdClientOptions Options { get; } = options.Value;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;
    private IProviderKeySetResolver KeySetResolver { get; } = keySetResolver;
    private IJwtValidator JwtValidator { get; } = jwtValidator;

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
            return Fail("The ID token is empty.");

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var issuer =
            metadata.Issuer
            ?? throw new OpenIdProviderException(
                "The discovery document of the provider lacks the issuer."
            );

        var algorithms = GetPermittedAlgorithms(metadata);
        if (algorithms.Count == 0)
            return Fail(
                "No ID-token signing algorithm is both permitted by the client and advertised by the provider."
            );

        var parameters = new JwtValidationParameters
        {
            ClockSkew = ClockSkew,
            ValidSignatureAlgorithms = algorithms,
            ResolveValidationKeysAsync = (token, _, _, ct) =>
                ResolveKeysAsync(registration.Authority, token, ct),
        }
            .ValidateIssuer(issuer)
            .ValidateAudience(registration.ClientId)
            .ValidateTokenLifeTime(o => o.RequireIssuedAt = true);

        var result = await JwtValidator.ValidateAsync(
            request.IdToken,
            parameters,
            cancellationToken
        );

        if (!result.IsValid)
        {
            var exception = result.Exception;
            if (exception is OperationCanceledException or OpenIdProviderException)
                ExceptionDispatchInfo.Capture(exception).Throw();

            return Fail(
                exception is JwtDecodeException or JwtSecretKeyNotFoundException
                    ? "The ID token signature could not be verified with the provider's signing keys."
                    : "The ID token is invalid: " + exception.Message
            );
        }

        var header = result.DecodedJwt.Header;
        var payload = result.DecodedJwt.Payload;

        if (
            header.TryGetProperty(JoseClaimNames.Header.Typ, out var typ)
            && (
                typ.ValueKind != JsonValueKind.String
                || !JoseTokenTypes.AreSame(typ.GetString(), JoseTokenTypes.Jwt)
            )
        )
            return Fail("The 'typ' header of the ID token is not JWT.");

        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Sub, out var sub)
            || sub.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(sub.GetString())
        )
            return Fail("The ID token has no 'sub' claim.");

        if (
            request.Subject is { } expectedSubject
            && !string.Equals(sub.GetString(), expectedSubject, StringComparison.Ordinal)
        )
            return Fail("The 'sub' claim of the ID token does not match the expected subject.");

        if (
            payload.TryGetProperty(JoseClaimNames.Payload.Azp, out var azp)
            && (
                azp.ValueKind != JsonValueKind.String
                || !string.Equals(azp.GetString(), registration.ClientId, StringComparison.Ordinal)
            )
        )
            return Fail("The 'azp' claim of the ID token does not match the client.");

        if (
            request.Nonce is { } expectedNonce
            && (
                !payload.TryGetProperty(JoseClaimNames.Payload.Nonce, out var nonce)
                || nonce.ValueKind != JsonValueKind.String
                || !string.Equals(nonce.GetString(), expectedNonce, StringComparison.Ordinal)
            )
        )
            return Fail("The 'nonce' claim of the ID token does not match the request.");

        if (request.MaxAge is { } maxAge)
        {
            if (
                !payload.TryGetProperty(JoseClaimNames.Payload.AuthTime, out var authTimeElement)
                || authTimeElement.ValueKind != JsonValueKind.Number
                || !authTimeElement.TryGetDouble(out var authTimeSeconds)
            )
                return Fail("The ID token has no numeric 'auth_time' claim.");

            var authTime = DateTimeOffset.UnixEpoch.AddSeconds(authTimeSeconds);
            if (TimeProvider.GetUtcNow() > authTime + maxAge + ClockSkew)
                return Fail("The authentication of the ID token is older than the maximum age.");
        }

        if (
            !string.IsNullOrEmpty(request.AccessToken)
            || !string.IsNullOrEmpty(request.AuthorizationCode)
            || !string.IsNullOrEmpty(request.State)
        )
        {
            if (
                !header.TryGetProperty(JoseClaimNames.Header.Alg, out var algElement)
                || algElement.ValueKind != JsonValueKind.String
                || algElement.GetString() is not { } headerAlg
                || !AlgorithmCollectionProvider.Collection.TryGetSignatureAlgorithm(
                    headerAlg,
                    out var signatureAlgorithm
                )
            )
                return Fail("The ID token signing algorithm is not registered.");

            var hashFunction = signatureAlgorithm.GetTokenHashFunction(
                result.DecodedJwt.SigningKey,
                out var hashSize
            );

            if (
                CheckHash(
                    payload,
                    JoseClaimNames.Payload.AtHash,
                    request.AccessToken,
                    hashFunction,
                    hashSize
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
                    hashSize
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
                    hashSize
                ) is
                { } sHashFailure
            )
                return sHashFailure;
        }

        return new IdTokenValidationResult(payload);
    }

    private static IdTokenValidationResult? CheckHash(
        JsonElement payload,
        string claimName,
        string? value,
        HashFunctionDelegate hashFunction,
        int hashSize
    )
    {
        if (string.IsNullOrEmpty(value) || !payload.TryGetProperty(claimName, out var claim))
            return null;

        if (
            claim.ValueKind != JsonValueKind.String
            || claim.GetString() is not { } tokenHash
            || !TokenHash.Verify(hashFunction, hashSize, value, tokenHash)
        )
            return Fail($"The ID token '{claimName}' does not match.");

        return null;
    }

    private static IdTokenValidationResult Fail(string reason) => new(reason);

    private List<string> GetPermittedAlgorithms(OpenIdProviderMetadata metadata)
    {
        var configured = Options.IdTokenSigningAlgorithms;
        var advertised = metadata.IdTokenSigningAlgValuesSupported;

        var result = new List<string>();
        foreach (var algorithm in AlgorithmCollectionProvider.Collection)
        {
            if (
                algorithm is not SignatureAlgorithm signatureAlgorithm
                || !typeof(AsymmetricSecretKey).IsAssignableFrom(signatureAlgorithm.KeyType)
                || string.Equals(
                    signatureAlgorithm.Code,
                    NoneAlgorithm,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;

            var code = signatureAlgorithm.Code;
            if (configured is { Count: > 0 } && !configured.Contains(code, StringComparer.Ordinal))
                continue;

            if (advertised is { Count: > 0 } && !advertised.Contains(code, StringComparer.Ordinal))
                continue;

            result.Add(code);
        }

        return result;
    }

    private async ValueTask<IEnumerable<SecretKey>> ResolveKeysAsync(
        string authority,
        JoseToken token,
        CancellationToken cancellationToken
    )
    {
        var keys = await KeySetResolver.GetSigningKeysAsync(authority, cancellationToken);

        if (
            !token.Header.TryGetProperty(JoseClaimNames.Header.Kid, out var kidElement)
            || kidElement.ValueKind != JsonValueKind.String
            || kidElement.GetString() is not { Length: > 0 } kid
        )
            return keys;

        if (keys.TryGetByKeyId(kid, out var key))
            return [key];

        keys = await KeySetResolver.RefreshSigningKeysAsync(authority, cancellationToken);
        return keys.TryGetByKeyId(kid, out key) ? [key] : [];
    }
}
