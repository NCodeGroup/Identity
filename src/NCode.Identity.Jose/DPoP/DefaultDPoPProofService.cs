#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Json;

namespace NCode.Identity.Jose.DPoP;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPProofService"/> abstraction (RFC 9449 §4). The proof's
/// embedded public <c>jwk</c> is imported through the registered secret key types and its signature is verified by the
/// registered JOSE signature algorithms, so every asymmetric key type and algorithm the JOSE stack supports is
/// available to DPoP without a parallel cryptographic code path.
/// </summary>
internal class DefaultDPoPProofService(
    TimeProvider timeProvider,
    IJoseSerializer joseSerializer,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : IDPoPProofService
{
    private const int JtiSizeBytes = 16;

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public string CreateProof(DPoPProofCreationOptions options)
    {
        var signingCredentials = options.SigningCredentials;
        var secretKey = signingCredentials.SecretKey;
        if (secretKey is not AsymmetricSecretKey)
        {
            throw new ArgumentException(
                "A DPoP proof must be signed with an asymmetric key.",
                nameof(options)
            );
        }

        if (
            !SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor)
            || descriptor.ExportPublicKey(secretKey) is not { } exportedPublicKey
        )
        {
            throw new ArgumentException(
                "The DPoP signing key's public key cannot be exported as a JSON Web Key.",
                nameof(options)
            );
        }

        if (!TryGetRequestUri(options.HttpUri, out var htu))
        {
            throw new ArgumentException(
                "The DPoP proof's HTTP URI must be an absolute HTTP or HTTPS URI.",
                nameof(options)
            );
        }

        var payload = new Dictionary<string, object>
        {
            [JoseClaimNames.Payload.Htm] = options.HttpMethod,
            [JoseClaimNames.Payload.Htu] = htu,
            [JoseClaimNames.Payload.Jti] = Base64Url.Encode(
                RandomNumberGenerator.GetBytes(JtiSizeBytes)
            ),
            [JoseClaimNames.Payload.Iat] = TimeProvider.GetUtcNow().ToUnixTimeSeconds(),
        };

        if (!string.IsNullOrEmpty(options.AccessToken))
            payload[JoseClaimNames.Payload.Ath] = ComputeAccessTokenHash(options.AccessToken);

        if (!string.IsNullOrEmpty(options.Nonce))
            payload[JoseClaimNames.Payload.Nonce] = options.Nonce;

        // The proof carries its public key in the 'jwk' header instead of a 'kid' (RFC 9449 §4.2).
        var signature = new JoseSignatureOptions(signingCredentials)
        {
            TokenType = JoseTokenTypes.DPoPJwt,
            AddKeyIdHeader = false,
            ProtectedHeaders =
            [
                new KeyValuePair<string, object>(
                    JoseClaimNames.Header.Jwk,
                    exportedPublicKey.Jwk.AsElement()
                ),
            ],
        };

        var encoder = JoseSerializer.CreateEncoder(new JoseSigningOptions(signature));
        return encoder.Encode(payload);
    }

    /// <inheritdoc />
    public bool TryValidateProof(
        string proofJwt,
        DPoPProofValidationParameters parameters,
        [NotNullWhen(true)] out DPoPProof? proof,
        [NotNullWhen(false)] out string? error
    )
    {
        proof = null;

        // A DPoP proof is a compact JWS: header.payload.signature (RFC 9449 §4.2).
        var parts = proofJwt.Split('.');
        if (parts.Length != 3)
        {
            error = "The DPoP proof is not a well-formed compact JWS.";
            return false;
        }

        if (
            !TryDecodeObject(parts[0], out var header)
            || !TryDecodeObject(parts[1], out var payload)
        )
        {
            error = "The DPoP proof header or payload could not be decoded.";
            return false;
        }

        // typ MUST be "dpop+jwt" (RFC 9449 §4.3).
        if (
            !header.TryGetPropertyValue<string>(JoseClaimNames.Header.Typ, out var typ)
            || !string.Equals(typ, JoseTokenTypes.DPoPJwt, StringComparison.Ordinal)
        )
        {
            error = "The DPoP proof 'typ' header must be 'dpop+jwt'.";
            return false;
        }

        // No JWS extension is understood here, so a proof that marks any header as critical is rejected (RFC 7515
        // §4.1.11).
        if (header.TryGetProperty(JoseClaimNames.Header.Crit, out _))
        {
            error = "The DPoP proof 'crit' header is not supported.";
            return false;
        }

        // alg MUST be an asymmetric signature algorithm the caller accepts; never 'none' or a MAC (RFC 9449 §4.3).
        if (
            !header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var alg)
            || string.IsNullOrEmpty(alg)
        )
        {
            error = "The DPoP proof is missing the 'alg' header.";
            return false;
        }

        if (
            !parameters.AllowedAlgorithms.Contains(alg, StringComparer.Ordinal)
            || !AlgorithmCollectionProvider.Collection.TryGetSignatureAlgorithm(
                alg,
                out var signatureAlgorithm
            )
            || !typeof(AsymmetricSecretKey).IsAssignableFrom(signatureAlgorithm.KeyType)
        )
        {
            error = $"The DPoP proof 'alg' header '{alg}' is not supported.";
            return false;
        }

        if (!TryImportPublicKey(header, out var jwk, out var secretKey, out error))
            return false;

        if (!signatureAlgorithm.KeyType.IsInstanceOfType(secretKey))
        {
            error = $"The DPoP proof 'alg' header '{alg}' does not match the 'jwk' key type.";
            return false;
        }

        if (!KeySizesUtility.IsLegalSize(signatureAlgorithm.KeyBitSizes, secretKey.KeySizeBits))
        {
            error = $"The DPoP proof 'jwk' key size is not supported by the '{alg}' algorithm.";
            return false;
        }

        if (!TryVerifySignature(signatureAlgorithm, secretKey, parts, out error))
            return false;

        // RFC 7638: the thumbprint is computed over the key type's required members in canonical form.
        string jwkThumbprint;
        try
        {
            jwkThumbprint = jwk.ComputeThumbprint();
        }
        catch (Exception exception)
            when (exception is NotSupportedException or InvalidOperationException)
        {
            error = "The DPoP proof 'jwk' is missing members required to compute its thumbprint.";
            return false;
        }

        if (!TryValidateClaims(payload, parameters, out var jti, out var issuedAt, out error))
            return false;

        payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Nonce, out var nonce);

        proof = new DPoPProof
        {
            Algorithm = alg,
            PublicKey = jwk,
            JwkThumbprint = jwkThumbprint,
            Jti = jti,
            IssuedAt = issuedAt,
            Nonce = string.IsNullOrEmpty(nonce) ? null : nonce,
        };
        return true;
    }

    private bool TryImportPublicKey(
        JsonElement header,
        [NotNullWhen(true)] out JsonWebKey? jwk,
        [NotNullWhen(true)] out SecretKey? secretKey,
        [NotNullWhen(false)] out string? error
    )
    {
        jwk = null;
        secretKey = null;

        // The embedded public JWK is the key the proof is signed with and an access token is bound to; it MUST NOT
        // carry any private key material (RFC 9449 §4.3).
        if (
            !header.TryGetProperty(JoseClaimNames.Header.Jwk, out var jwkElement)
            || jwkElement.ValueKind != JsonValueKind.Object
        )
        {
            error = "The DPoP proof is missing the 'jwk' header.";
            return false;
        }

        try
        {
            jwk = new JsonWebKey(jwkElement);
        }
        catch (ArgumentException)
        {
            error = "The DPoP proof 'jwk' header is not a valid JSON Web Key.";
            return false;
        }

        if (jwk.ContainsPrivateKeyMaterial)
        {
            error = "The DPoP proof 'jwk' header must not contain private key material.";
            return false;
        }

        if (
            !SecretKeyTypeCatalog.TryGetByJwkKeyType(jwk.KeyType, out var descriptor)
            || !descriptor.PublicKeyImportFormats.Contains(SecretKeyImportFormats.Jwk)
        )
        {
            error = $"The DPoP proof 'jwk' key type '{jwk.KeyType}' is not supported.";
            return false;
        }

        try
        {
            byte[] keyMaterial = [];
            var imported = descriptor.ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = jwkElement.GetRawText(),
                },
                bytes =>
                {
                    keyMaterial = bytes.ToArray();
                    return string.Empty;
                }
            );
            secretKey = descriptor.DeserializeKeyMaterial(
                new KeyMetadata(),
                imported.SecretEncoding,
                keyMaterial
            );
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or FormatException
                        or JsonException
                        or NotSupportedException
                        or CryptographicException
            )
        {
            jwk = null;
            error = "The DPoP proof 'jwk' header is not a valid public key.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryVerifySignature(
        SignatureAlgorithm signatureAlgorithm,
        SecretKey secretKey,
        string[] parts,
        [NotNullWhen(false)] out string? error
    )
    {
        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        bool verified;
        try
        {
            var signature = DecodeBase64Url(parts[2]);
            verified =
                signature.Length == signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits)
                && signatureAlgorithm.Verify(secretKey, signingInput, signature);
        }
        catch (Exception exception)
            when (exception is ArgumentException or FormatException or CryptographicException)
        {
            verified = false;
        }

        error = verified ? null : "The DPoP proof signature is invalid.";
        return verified;
    }

    private bool TryValidateClaims(
        JsonElement payload,
        DPoPProofValidationParameters parameters,
        [NotNullWhen(true)] out string? jti,
        out DateTimeOffset issuedAt,
        [NotNullWhen(false)] out string? error
    )
    {
        jti = null;
        issuedAt = default;

        // htm MUST equal the request method (RFC 9449 §4.3).
        if (
            !payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Htm, out var htm)
            || !string.Equals(htm, parameters.HttpMethod, StringComparison.Ordinal)
        )
        {
            error = "The DPoP proof 'htm' claim does not match the request method.";
            return false;
        }

        // htu MUST equal the request URI with any query and fragment removed (RFC 9449 §4.3).
        if (
            !payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Htu, out var htu)
            || !TryGetRequestUri(htu, out var proofUri)
            || !TryGetRequestUri(parameters.HttpUri, out var requestUri)
            || !string.Equals(proofUri, requestUri, StringComparison.Ordinal)
        )
        {
            error = "The DPoP proof 'htu' claim does not match the request URI.";
            return false;
        }

        // ath, when the proof accompanies an access token at a protected resource, MUST equal the base64url SHA-256
        // hash of that access token (RFC 9449 §4.3, §7.1).
        if (
            !string.IsNullOrEmpty(parameters.AccessToken)
            && (
                !payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Ath, out var ath)
                || !string.Equals(
                    ath,
                    ComputeAccessTokenHash(parameters.AccessToken),
                    StringComparison.Ordinal
                )
            )
        )
        {
            error = "The DPoP proof 'ath' claim does not match the access token.";
            return false;
        }

        // jti MUST be present so the caller can track the proof for replay (RFC 9449 §4.3, §11.1).
        if (
            !payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Jti, out jti)
            || string.IsNullOrEmpty(jti)
        )
        {
            jti = null;
            error = "The DPoP proof is missing the 'jti' claim.";
            return false;
        }

        // iat MUST be a valid NumericDate within the acceptable freshness window (RFC 9449 §4.3, §11.1).
        if (!payload.TryGetPropertyNumericDate(JoseClaimNames.Payload.Iat, out issuedAt))
        {
            jti = null;
            error = "The DPoP proof is missing a valid 'iat' claim.";
            return false;
        }

        var now = TimeProvider.GetUtcNow();
        if (issuedAt > now + parameters.Lifetime || issuedAt < now - parameters.Lifetime)
        {
            jti = null;
            error = "The DPoP proof 'iat' claim is outside the acceptable window.";
            return false;
        }

        error = null;
        return true;
    }

    private static string ComputeAccessTokenHash(string accessToken) =>
        Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)));

    // Scheme, authority, and path, ignoring query and fragment (RFC 9449 §4.2, §4.3).
    private static bool TryGetRequestUri(string? value, [NotNullWhen(true)] out string? requestUri)
    {
        // On Unix a rooted path such as "/token" parses as an absolute file URI, so the scheme is checked too.
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        )
        {
            requestUri = null;
            return false;
        }

        requestUri = uri.GetLeftPart(UriPartial.Path);
        return true;
    }

    private static bool TryDecodeObject(string encoded, out JsonElement element)
    {
        try
        {
            using var document = JsonDocument.Parse(DecodeBase64Url(encoded));
            element = document.RootElement.Clone();
            return element.ValueKind == JsonValueKind.Object;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            element = default;
            return false;
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var byteCount = Base64Url.GetByteCountForDecode(value.Length);
        var buffer = new byte[byteCount];
        if (!Base64Url.TryDecode(value, buffer, out var bytesWritten))
            throw new FormatException("The value is not valid base64url.");
        return bytesWritten == byteCount ? buffer : buffer[..bytesWritten];
    }
}
