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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Secrets;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides a default implementation for resolving the <see cref="SecretKey"/> instances that are to be used
/// to validate a Json Web Token (JWT).
/// </summary>
public static class DefaultValidationKeyResolver
{
    /// <summary>
    /// Provides a default implementation for resolving the <see cref="SecretKey"/> instances that are to be used
    /// to validate a Json Web Token (JWT).
    /// </summary>
    /// <param name="protectionType">A value indicating how the JWT is protected, either 'JWS' or 'JWE'.</param>
    /// <param name="header">A <see cref="JsonElement"/> instance that contains the Json Web Token (JWT) header.</param>
    /// <param name="secretKeys">A <see cref="ISecretKeyCollection"/> instance that contains the candidate <see cref="SecretKey"/> instances.</param>
    /// <returns>A collection of <see cref="SecretKey"/> instances.</returns>
    public static IEnumerable<SecretKey> ResolveValidationKeys(
        string protectionType,
        JsonElement header,
        ISecretKeyCollection secretKeys)
    {
        // attempt to lookup by 'kid'
        if (header.TryGetPropertyValue<string>(JoseClaimNames.Header.Kid, out var keyId) &&
            secretKeys.TryGetByKeyId(keyId, out var specificKey))
        {
            return new[] { specificKey };
        }

        // attempt to lookup by certificate thumbprint
        var hasThumbprintSha1 = header.TryGetPropertyValue<string>(JoseClaimNames.Header.X5t, out var thumbprintSha1);
        var hasThumbprintSha256 = header.TryGetPropertyValue<string>(JoseClaimNames.Header.X5tS256, out var thumbprintSha256);

        if (hasThumbprintSha1 && secretKeys.TryGetByKeyId(thumbprintSha1!, out specificKey))
        {
            return new[] { specificKey };
        }

        if (hasThumbprintSha256 && secretKeys.TryGetByKeyId(thumbprintSha256!, out specificKey))
        {
            return new[] { specificKey };
        }

        if (hasThumbprintSha1 || hasThumbprintSha256)
        {
            const int sha1HashSize = 20;
            const int sha256HashSize = 32;

            var expectedSha1 = hasThumbprintSha1 ? stackalloc byte[sha1HashSize] : default;
            if (hasThumbprintSha1)
            {
                var result = Base64Url.TryDecode(thumbprintSha1, expectedSha1, out var bytesWritten);
                Debug.Assert(result && bytesWritten == sha1HashSize);
            }

            var expectedSha256 = hasThumbprintSha256 ? stackalloc byte[sha256HashSize] : default;
            if (hasThumbprintSha256)
            {
                var result = Base64Url.TryDecode(thumbprintSha256, expectedSha256, out var bytesWritten);
                Debug.Assert(result && bytesWritten == sha256HashSize);
            }

            Span<byte> actualHash = stackalloc byte[sha256HashSize];

            var keysWithCertificates = secretKeys.OfType<AsymmetricSecretKey>().Where(key => key.HasCertificate);
            foreach (var secretKey in keysWithCertificates)
            {
                using var certificate = secretKey.ExportCertificate();
                Debug.Assert(certificate is not null);

                if (hasThumbprintSha1 && VerifyCertificateHash(certificate, HashAlgorithmName.SHA1, expectedSha1, actualHash))
                {
                    return new[] { secretKey };
                }

                if (hasThumbprintSha256 && VerifyCertificateHash(certificate, HashAlgorithmName.SHA256, expectedSha256, actualHash))
                {
                    return new[] { secretKey };
                }
            }
        }

        // otherwise, the degenerate case will attempt to use all the keys with the specified usage and algorithm

        var use = protectionType switch
        {
            JoseProtectionTypes.Jws => SecretKeyUses.Signature,
            JoseProtectionTypes.Jwe => SecretKeyUses.Encryption,
            _ => protectionType
        };

        header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var algorithm);
        Debug.Assert(algorithm is not null);

        return secretKeys.Where(key =>
            (key.Metadata.Use is null || key.Metadata.Use == use) &&
            (key.Metadata.Algorithm is null || key.Metadata.Algorithm == algorithm));
    }

    private static bool VerifyCertificateHash(
        X509Certificate certificate,
        HashAlgorithmName hashAlgorithmName,
        ReadOnlySpan<byte> expected,
        Span<byte> actual)
    {
        if (expected.IsEmpty) return false;

        var result = certificate.TryGetCertHash(
            hashAlgorithmName,
            actual,
            out var bytesWritten);

        return result &&
               bytesWritten == expected.Length &&
               expected.SequenceEqual(actual[..bytesWritten]);
    }
}
