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

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.SharedState;
using NCode.Json;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="ILogoutTokenValidator"/> that first decrypts an encrypted logout
/// token with the client's keys, then verifies its signature with the provider's signing keys, checks its claims
/// (OpenID Connect Back-Channel Logout 1.0 §2.6) and refuses a token whose <c>jti</c> is already recorded;
/// <see cref="TryRecordHandledAsync"/> records it (ADR-0274, ADR-0284).
/// </summary>
internal class DefaultLogoutTokenValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IProviderMetadataResolver metadataResolver,
    IProviderKeySetResolver keySetResolver,
    IClientKeyRingProvider keyRingProvider,
    IJwtValidator jwtValidator,
    IExpiringKeySet expiringKeySet
) : ILogoutTokenValidator
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(10);

    private const string ReplayKeyPurpose = "logout_token_jti";

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;
    private IProviderKeySetResolver KeySetResolver { get; } = keySetResolver;
    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;

    /// <inheritdoc />
    public async ValueTask<LogoutTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        LogoutTokenValidationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.LogoutToken))
            return Fail(TokenValidationFailureCodes.InvalidToken, "The logout token is empty.");

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var issuer =
            metadata.Issuer
            ?? throw new OpenIdProviderException(
                "The discovery document of the provider lacks the issuer."
            );

        var collection = AlgorithmCollectionProvider.Collection;

        var algorithms = ProviderSignaturePolicy.GetPermittedAlgorithms(
            collection,
            registration,
            metadata
        );
        if (algorithms.Count == 0)
            return Fail(
                TokenValidationFailureCodes.AlgorithmNotPermitted,
                "No logout-token signing algorithm is both permitted by the client and advertised by the provider."
            );

        var keyManagementAlgorithms = ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms(
            collection,
            registration
        );
        var contentEncryptionAlgorithms =
            ClientDecryptionPolicy.GetPermittedContentEncryptionAlgorithms(
                collection,
                registration
            );
        var encryptionRequired = ClientDecryptionPolicy.IsEncryptionRegistered(registration);
        if (
            encryptionRequired
            && (keyManagementAlgorithms.Count == 0 || contentEncryptionAlgorithms.Count == 0)
        )
            return Fail(
                TokenValidationFailureCodes.AlgorithmNotPermitted,
                "No ID-token encryption algorithm that the client registered is permitted."
            );

        var signingKeysResolved = false;
        var parameters = new JwtValidationParameters
        {
            ClockSkew = ClockSkew,
            ValidSignatureAlgorithms = algorithms,
            ValidKeyManagementAlgorithms = keyManagementAlgorithms,
            ValidContentEncryptionAlgorithms = contentEncryptionAlgorithms,
            ValidCompressionAlgorithms = [],
            ResolveValidationKeysAsync = (token, _, _, ct) =>
            {
                // The signing keys are resolved only for the enclosed JWS, which exists only after decryption, so a
                // key failure before this point is a decryption failure (ADR-0280).
                signingKeysResolved = true;
                return ProviderSignaturePolicy.ResolveKeysAsync(
                    KeySetResolver,
                    registration.Authority,
                    token,
                    ct
                );
            },
        }
            .ValidateIssuer(issuer)
            .ValidateAudience(registration.ClientId)
            .ValidateTokenLifeTime(o =>
            {
                o.RequireIssuedAt = true;
                o.MaxLifetime = MaxLifetime;
            });

        if (encryptionRequired)
        {
            var decryptionKeys = ClientDecryptionPolicy.GetDecryptionKeys(
                KeyRingProvider,
                registration
            );
            parameters.UseDecryptionKeys(decryptionKeys);
        }

        var result = await JwtValidator.ValidateAsync(
            request.LogoutToken,
            parameters,
            cancellationToken
        );

        if (!result.IsValid)
        {
            var exception = result.Exception;
            ProviderSignaturePolicy.RethrowProviderFailure(exception);

            if (exception is JwtDecodeException or JwtSecretKeyNotFoundException)
                return signingKeysResolved
                    ? Fail(
                        TokenValidationFailureCodes.InvalidSignature,
                        "The logout token signature could not be verified with the provider's signing keys."
                    )
                    : Fail(
                        TokenValidationFailureCodes.DecryptionFailed,
                        "The logout token could not be decrypted with the client's keys."
                    );

            return Fail(result.FailureCode, "The logout token is invalid: " + exception.Message);
        }

        var encryptionHeader = result.DecodedJwt.EncryptionHeader;
        if (encryptionRequired && encryptionHeader is null)
            return Fail(
                TokenValidationFailureCodes.InvalidToken,
                "The logout token is not encrypted, but the client registered ID-token encryption."
            );

        if (!ClientDecryptionPolicy.IsReplicatedIssuerValid(encryptionHeader, issuer))
            return Fail(
                TokenValidationFailureCodes.InvalidIssuer,
                "The 'iss' header of the encrypted logout token does not match the issuer."
            );

        var header = result.DecodedJwt.Header;
        var payload = result.DecodedJwt.Payload;

        if (
            header.TryGetProperty(JoseClaimNames.Header.Typ, out var typ)
            && (
                typ.ValueKind != JsonValueKind.String
                || !JoseTokenTypes.AreSame(typ.GetString(), JoseTokenTypes.LogoutJwt)
            )
        )
            return Fail(
                TokenValidationFailureCodes.InvalidTokenType,
                "The 'typ' header of the logout token is not logout+jwt."
            );

        string? sub = null;
        if (payload.TryGetProperty(JoseClaimNames.Payload.Sub, out var subElement))
        {
            if (
                subElement.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(sub = subElement.GetString())
            )
                return Fail(
                    TokenValidationFailureCodes.InvalidClaim,
                    "The 'sub' claim of the logout token is not a non-empty string."
                );
        }

        string? sid = null;
        if (payload.TryGetProperty(JoseClaimNames.Payload.Sid, out var sidElement))
        {
            if (
                sidElement.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(sid = sidElement.GetString())
            )
                return Fail(
                    TokenValidationFailureCodes.InvalidClaim,
                    "The 'sid' claim of the logout token is not a non-empty string."
                );
        }

        if (sub is null && sid is null)
            return Fail(
                TokenValidationFailureCodes.InvalidClaim,
                "The logout token has neither a 'sub' nor a 'sid' claim."
            );

        if (registration.BackchannelLogoutSessionRequired && sid is null)
            return Fail(
                TokenValidationFailureCodes.InvalidClaim,
                "The logout token has no 'sid' claim, which the client requires."
            );

        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Events, out var events)
            || events.ValueKind != JsonValueKind.Object
            || !events.TryGetProperty(
                OpenIdProtocol.BackChannelLogout.EventName,
                out var logoutEvent
            )
            || logoutEvent.ValueKind != JsonValueKind.Object
        )
            return Fail(
                TokenValidationFailureCodes.InvalidClaim,
                "The 'events' claim of the logout token does not contain the back-channel logout event."
            );

        if (payload.TryGetProperty(JoseClaimNames.Payload.Nonce, out _))
            return Fail(
                TokenValidationFailureCodes.InvalidClaim,
                "The logout token contains a 'nonce' claim."
            );

        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Jti, out var jtiElement)
            || jtiElement.ValueKind != JsonValueKind.String
            || jtiElement.GetString() is not { Length: > 0 } jti
        )
            return Fail(
                TokenValidationFailureCodes.InvalidClaim,
                "The logout token has no 'jti' claim."
            );

        if (!payload.TryGetPropertyNumericDate(JoseClaimNames.Payload.Exp, out var expiresAt))
            return Fail(
                TokenValidationFailureCodes.InvalidLifetime,
                "The logout token has no valid 'exp' claim."
            );

        if (
            await ExpiringKeySet.ContainsAsync(
                CreateReplayKey(issuer, registration.ClientId, jti),
                cancellationToken
            )
        )
            return Fail(
                TokenValidationFailureCodes.TokenReplayed,
                "The logout token has already been handled."
            );

        return new LogoutTokenValidationResult(payload, issuer, jti, expiresAt, sub, sid);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryRecordHandledAsync(
        ClientRegistration registration,
        LogoutTokenValidationResult result,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsValid)
            throw new ArgumentException(
                "Only a valid logout token can be recorded as handled.",
                nameof(result)
            );

        return ExpiringKeySet.TryAddAsync(
            CreateReplayKey(result.Issuer, registration.ClientId, result.TokenId),
            result.ExpiresAt + ClockSkew,
            cancellationToken
        );
    }

    private static string CreateReplayKey(string issuer, string clientId, string jti)
    {
        var material = string.Create(
            CultureInfo.InvariantCulture,
            $"{issuer.Length}:{issuer}{clientId.Length}:{clientId}{jti}"
        );

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(SecureEncoding.UTF8.GetBytes(material), digest);

        return ReplayKeyPurpose + ":" + Convert.ToHexStringLower(digest);
    }

    private static LogoutTokenValidationResult Fail(string failureCode, string reason) =>
        new(failureCode, reason);
}
