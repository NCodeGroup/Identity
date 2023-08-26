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
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose;
using NCode.Jose.Json;

namespace NCode.Identity.Jwt;

public delegate ValueTask<ISecretKeyCollection> ResolveProviderKeysAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    ISecretKeyProvider secretKeyProvider,
    CancellationToken cancellationToken);

public delegate ValueTask<IEnumerable<string>> ResolveSecretKeyTagsAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    CancellationToken cancellationToken);

public delegate ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    ISecretKeyCollection candidateKeys,
    IEnumerable<string> secretKeyTags,
    CancellationToken cancellationToken);

public class ValidateJwtParameters
{
    /// <summary>
    /// Gets a <see cref="PropertyBag"/> that can be used to store custom state information.
    /// This instance will be cloned for each JWT operation.
    /// </summary>
    public PropertyBag PropertyBag { get; } = new();

    // TODO: add similar resolvers for these
    public string NameClaimType { get; set; } = ClaimsIdentity.DefaultNameClaimType;
    public string RoleClaimType { get; set; } = ClaimsIdentity.DefaultRoleClaimType;

    public ResolveProviderKeysAsync ResolveProviderKeysAsync { get; set; } =
        (_, _, secretKeyProvider, _) => ValueTask.FromResult(
            secretKeyProvider.SecretKeys);

    public ResolveSecretKeyTagsAsync ResolveSecretKeyTagsAsync { get; set; } =
        // use an empty collection that has Count=0 to leverage the optimized code path in IsSupersetOf
        (_, _, _) => ValueTask.FromResult<IEnumerable<string>>(
            Array.Empty<string>());

    public ResolveValidationKeysAsync ResolveValidationKeysAsync { get; set; } =
        (compactJwt, _, candidateKeys, secretKeyTags, _) => ValueTask.FromResult(
            DefaultResolveValidationKeys(
                compactJwt,
                candidateKeys,
                secretKeyTags));

    public ICollection<IValidateJwtHandler> Handlers { get; } = new List<IValidateJwtHandler>();

    private static IEnumerable<SecretKey> DefaultResolveValidationKeys(
        CompactJwt compactJwt,
        ISecretKeyCollection candidateKeys,
        IEnumerable<string> secretKeyTags)
    {
        var header = compactJwt.DeserializedHeader;

        // attempt to lookup by 'kid'
        if (header.TryGetPropertyValue<string>(JwtClaimNames.Kid, out var keyId) &&
            candidateKeys.TryGetByKeyId(keyId, out var specificKey))
        {
            return new[] { specificKey };
        }

        // attempt to lookup by certificate thumbprint
        var hasThumbprintSha1 = header.TryGetPropertyValue<string>(JwtClaimNames.X5t, out var thumbprintSha1);
        var hasThumbprintSha256 = header.TryGetPropertyValue<string>(JwtClaimNames.X5tS256, out var thumbprintSha256);

        if (hasThumbprintSha1 && candidateKeys.TryGetByKeyId(thumbprintSha1!, out specificKey))
        {
            return new[] { specificKey };
        }

        if (hasThumbprintSha256 && candidateKeys.TryGetByKeyId(thumbprintSha256!, out specificKey))
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

            foreach (var secretKey in candidateKeys)
            {
                if (secretKey is not AsymmetricSecretKey { Certificate: not null } asymmetricSecretKey) continue;
                var certificate = asymmetricSecretKey.Certificate;

                if (VerifyCertificateHash(certificate, HashAlgorithmName.SHA1, expectedSha1, actualHash))
                {
                    return new[] { secretKey };
                }

                if (VerifyCertificateHash(certificate, HashAlgorithmName.SHA256, expectedSha256, actualHash))
                {
                    return new[] { secretKey };
                }
            }
        }

        // otherwise, the degenerate case will attempt to use the keys with the specified tags
        return candidateKeys.GetByTags(secretKeyTags);
    }

    private static bool VerifyCertificateHash(
        X509Certificate certificate,
        HashAlgorithmName hashAlgorithmName,
        ReadOnlySpan<byte> expected,
        Span<byte> actual)
    {
        if (expected.IsEmpty) return false;

        var result = certificate.TryGetCertHash(hashAlgorithmName, actual, out var bytesWritten);
        Debug.Assert(result);

        return expected.SequenceEqual(actual[..bytesWritten]);
    }
}
