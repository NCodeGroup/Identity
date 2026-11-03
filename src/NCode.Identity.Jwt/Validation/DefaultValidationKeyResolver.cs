// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides a default implementation for resolving the <see cref="SecretKey"/> instances that are to be used
/// to validate a JSON Web Token (JWT).
/// </summary>
internal static class DefaultValidationKeyResolver
{
    /// <summary>
    /// Provides a default implementation for resolving the <see cref="SecretKey"/> instances that are to be used
    /// to validate a JSON Web Token (JWT).
    /// </summary>
    /// <param name="protectionType">A value indicating how the JWT is protected, either 'JWS' or 'JWE'.</param>
    /// <param name="header">A <see cref="JsonElement"/> instance that contains the JSON Web Token (JWT) header.</param>
    /// <param name="secretKeys">A <see cref="ISecretKeyCollection"/> instance that contains the candidate <see cref="SecretKey"/> instances.</param>
    /// <returns>A collection of <see cref="SecretKey"/> instances.</returns>
    public static IEnumerable<SecretKey> ResolveValidationKeys(
        string protectionType,
        JsonElement header,
        ISecretKeyCollection secretKeys
    )
    {
        // attempt to lookup by 'kid'
        if (
            header.TryGetPropertyValue<string>(JoseClaimNames.Header.Kid, out var keyId)
            && secretKeys.TryGetByKeyId(keyId, out var specificKey)
        )
        {
            return [specificKey];
        }

        // attempt to lookup by certificate thumbprint
        var hasThumbprintSha1 = header.TryGetPropertyValue<string>(
            JoseClaimNames.Header.X5t,
            out var thumbprintSha1
        );
        var hasThumbprintSha256 = header.TryGetPropertyValue<string>(
            JoseClaimNames.Header.X5tS256,
            out var thumbprintSha256
        );

        if (
            hasThumbprintSha1
            && thumbprintSha1 is not null
            && secretKeys.TryGetByKeyId(thumbprintSha1, out specificKey)
        )
        {
            return [specificKey];
        }

        if (
            hasThumbprintSha256
            && thumbprintSha256 is not null
            && secretKeys.TryGetByKeyId(thumbprintSha256, out specificKey)
        )
        {
            return [specificKey];
        }

        if (hasThumbprintSha1 || hasThumbprintSha256)
        {
            const int sha1HashSize = 20;
            const int sha256HashSize = 32;

            var expectedSha1 = hasThumbprintSha1 ? stackalloc byte[sha1HashSize] : default;
            if (hasThumbprintSha1)
            {
                DecodeThumbprint(JoseClaimNames.Header.X5t, thumbprintSha1, expectedSha1);
            }

            var expectedSha256 = hasThumbprintSha256 ? stackalloc byte[sha256HashSize] : default;
            if (hasThumbprintSha256)
            {
                DecodeThumbprint(JoseClaimNames.Header.X5tS256, thumbprintSha256, expectedSha256);
            }

            Span<byte> actualHash = stackalloc byte[sha256HashSize];

            var keysWithCertificates = secretKeys
                .OfType<AsymmetricSecretKey>()
                .Where(key => key.HasCertificate);
            foreach (var secretKey in keysWithCertificates)
            {
                using var certificate = secretKey.ExportCertificate();
                Debug.Assert(certificate is not null);

                if (
                    hasThumbprintSha1
                    && VerifyCertificateHash(
                        certificate,
                        HashAlgorithmName.SHA1,
                        expectedSha1,
                        actualHash
                    )
                )
                {
                    return [secretKey];
                }

                if (
                    hasThumbprintSha256
                    && VerifyCertificateHash(
                        certificate,
                        HashAlgorithmName.SHA256,
                        expectedSha256,
                        actualHash
                    )
                )
                {
                    return [secretKey];
                }
            }
        }

        // otherwise, the degenerate case will attempt to use all the keys with the specified usage and algorithm

        var use = protectionType switch
        {
            JoseProtectionTypes.Jws => SecretKeyUses.Signature,
            JoseProtectionTypes.Jwe => SecretKeyUses.Encryption,
            _ => protectionType,
        };

        header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var algorithm);
        Debug.Assert(algorithm is not null);

        return secretKeys.Where(key =>
            (key.Metadata.Use is null || key.Metadata.Use == use)
            && (key.Metadata.Algorithm is null || key.Metadata.Algorithm == algorithm)
        );
    }

    // A thumbprint is read from the token's header, so one that does not decode to exactly its digest's size refuses the
    // token, and no certificate is compared with a digest the decoder wrote only in part.
    private static void DecodeThumbprint(string name, string? encoded, Span<byte> digest)
    {
        bool decoded;
        var bytesWritten = 0;
        try
        {
            decoded =
                encoded is not null
                && encoded.Length == Base64Url.GetCharCountForEncode(digest.Length)
                && Base64Url.TryDecode(encoded, digest, out bytesWritten);
        }
        catch (FormatException exception)
        {
            throw new JwtValidationException(
                $"The '{name}' header is not a base64url-encoded certificate thumbprint.",
                exception
            );
        }

        if (!decoded || bytesWritten != digest.Length)
        {
            throw new JwtValidationException(
                $"The '{name}' header is not a base64url-encoded certificate thumbprint."
            );
        }
    }

    private static bool VerifyCertificateHash(
        X509Certificate certificate,
        HashAlgorithmName hashAlgorithmName,
        ReadOnlySpan<byte> expected,
        Span<byte> actual
    )
    {
        if (expected.IsEmpty)
            return false;

        var result = certificate.TryGetCertHash(hashAlgorithmName, actual, out var bytesWritten);

        return result
            && bytesWritten == expected.Length
            && expected.SequenceEqual(actual[..bytesWritten]);
    }
}
