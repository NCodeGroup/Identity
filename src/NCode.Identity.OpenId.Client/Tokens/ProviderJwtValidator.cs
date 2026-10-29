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
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Validates a JWT that the provider issues to the client, such as an ID token, a logout token or a signed UserInfo
/// response: it decrypts the token with the client's keys when the client registered encryption for it, verifies its
/// signature with the provider's signing keys under the algorithms both sides permit, and checks its issuer, audience,
/// lifetime and <c>typ</c> as its <see cref="ProviderJwtProfile"/> says. The caller checks the claims particular to the
/// kind of token.
/// </summary>
// See ADR-0301.
internal class ProviderJwtValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IProviderKeySetResolver keySetResolver,
    IClientKeyRingProvider keyRingProvider,
    IJwtValidator jwtValidator
)
{
    /// <summary>
    /// The clock skew allowed when comparing a token's times with the current time.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private IProviderKeySetResolver KeySetResolver { get; } = keySetResolver;
    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;
    private IJwtValidator JwtValidator { get; } = jwtValidator;

    /// <summary>
    /// Validates a JWT that the provider issued to the client.
    /// </summary>
    /// <param name="token">The compact JWT.</param>
    /// <param name="issuer">The issuer identifier of the provider.</param>
    /// <param name="profile">The kind of token and how the client registered its protection.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The validated token, or the reason it is invalid.</returns>
    /// <exception cref="OpenIdProviderException">The provider's signing keys could not be retrieved.</exception>
    /// <exception cref="InvalidOperationException">The client's registration of the token's protection is
    /// inconsistent, or its key ring is missing.</exception>
    public async ValueTask<ProviderJwtValidationResult> ValidateAsync(
        string token,
        string issuer,
        ProviderJwtProfile profile,
        CancellationToken cancellationToken
    )
    {
        var registration = profile.Registration;
        var collection = AlgorithmCollectionProvider.Collection;

        var algorithms = ProviderSignaturePolicy.GetPermittedAlgorithms(
            collection,
            profile.SigningAlgorithms,
            profile.AdvertisedSigningAlgorithms
        );
        if (algorithms.Count == 0)
            return ProviderJwtValidationResult.Fail(
                TokenValidationFailureCodes.AlgorithmNotPermitted,
                $"No {profile.SigningName} signing algorithm is both permitted by the client and advertised by the provider."
            );

        var keyManagementAlgorithms = ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms(
            collection,
            profile
        );
        var contentEncryptionAlgorithms =
            ClientDecryptionPolicy.GetPermittedContentEncryptionAlgorithms(collection, profile);
        var encryptionRequired = ClientDecryptionPolicy.IsEncryptionRegistered(profile);
        if (
            encryptionRequired
            && (keyManagementAlgorithms.Count == 0 || contentEncryptionAlgorithms.Count == 0)
        )
            return ProviderJwtValidationResult.Fail(
                TokenValidationFailureCodes.AlgorithmNotPermitted,
                $"No {profile.EncryptionName} encryption algorithm that the client registered is permitted."
            );

        var signingKeysResolved = false;
        var parameters = new JwtValidationParameters
        {
            ClockSkew = ClockSkew,
            ValidSignatureAlgorithms = algorithms,
            ValidKeyManagementAlgorithms = keyManagementAlgorithms,
            ValidContentEncryptionAlgorithms = contentEncryptionAlgorithms,
            ValidCompressionAlgorithms = [],
            ResolveValidationKeysAsync = (jose, _, _, ct) =>
            {
                // The signing keys are resolved only for the enclosed JWS, which exists only after decryption, so a
                // key failure before this point is a decryption failure (ADR-0280).
                signingKeysResolved = true;
                return ProviderSignaturePolicy.ResolveKeysAsync(
                    KeySetResolver,
                    registration.Authority,
                    jose,
                    ct
                );
            },
        }
            .ValidateIssuer(issuer)
            .ValidateAudience(registration.ClientId)
            .ValidateTokenLifeTime(profile.ConfigureLifetime);

        if (encryptionRequired)
        {
            var decryptionKeys = ClientDecryptionPolicy.GetDecryptionKeys(KeyRingProvider, profile);
            parameters.UseDecryptionKeys(decryptionKeys);
        }

        var result = await JwtValidator.ValidateAsync(token, parameters, cancellationToken);

        if (!result.IsValid)
        {
            var exception = result.Exception;
            ProviderSignaturePolicy.RethrowProviderFailure(exception);

            if (exception is JwtDecodeException or JwtSecretKeyNotFoundException)
                return signingKeysResolved
                    ? ProviderJwtValidationResult.Fail(
                        TokenValidationFailureCodes.InvalidSignature,
                        $"The {profile.TokenName} signature could not be verified with the provider's signing keys."
                    )
                    : ProviderJwtValidationResult.Fail(
                        TokenValidationFailureCodes.DecryptionFailed,
                        $"The {profile.TokenName} could not be decrypted with the client's keys."
                    );

            return ProviderJwtValidationResult.Fail(
                result.FailureCode,
                $"The {profile.TokenName} is invalid: {exception.Message}"
            );
        }

        var encryptionHeader = result.DecodedJwt.EncryptionHeader;
        if (encryptionRequired && encryptionHeader is null)
            return ProviderJwtValidationResult.Fail(
                TokenValidationFailureCodes.InvalidToken,
                $"The {profile.TokenName} is not encrypted, but the client registered {profile.EncryptionName} encryption."
            );

        if (!ClientDecryptionPolicy.IsReplicatedIssuerValid(encryptionHeader, issuer))
            return ProviderJwtValidationResult.Fail(
                TokenValidationFailureCodes.InvalidIssuer,
                $"The 'iss' header of the encrypted {profile.TokenName} does not match the issuer."
            );

        if (
            result.DecodedJwt.Header.TryGetProperty(JoseClaimNames.Header.Typ, out var typ)
            && (
                typ.ValueKind != JsonValueKind.String
                || !JoseTokenTypes.AreSame(typ.GetString(), profile.TokenType)
            )
        )
            return ProviderJwtValidationResult.Fail(
                TokenValidationFailureCodes.InvalidTokenType,
                $"The 'typ' header of the {profile.TokenName} is not {profile.TokenType}."
            );

        return ProviderJwtValidationResult.Success(result);
    }
}
