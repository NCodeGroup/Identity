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
using System.Security.Cryptography.X509Certificates;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction.
/// </summary>
public class SecretSerializer : ISecretSerializer
{
    private ISecretKeyFactory SecretKeyFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretSerializer"/> class.
    /// </summary>
    /// <param name="secretKeyFactory">The <see cref="ISecretKeyFactory"/> instance.</param>
    public SecretSerializer(ISecretKeyFactory secretKeyFactory)
    {
        SecretKeyFactory = secretKeyFactory;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SecretKey> DeserializeSecrets(IEnumerable<Secret> secrets)
    {
        var results = new SortedSet<SecretKey>(SecretKeyExpiresWhenComparer.Singleton);
        try
        {
            foreach (var secret in secrets)
            {
                results.Add(DeserializeSecret(secret));
            }
        }
        catch
        {
            results.DisposeAll(ignoreExceptions: true);
            throw;
        }

        return results;
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(Secret secret)
    {
        var metadata = new KeyMetadata
        {
            TenantId = secret.TenantId,
            KeyId = secret.SecretId,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            ExpiresWhen = secret.ExpiresWhen
        };
        return secret.SecretType switch
        {
            SecretConstants.SecretTypes.Certificate => DeserializeCertificate(metadata, secret),
            SecretConstants.SecretTypes.Symmetric => DeserializeSymmetric(metadata, secret),
            SecretConstants.SecretTypes.Rsa => DeserializeRsa(metadata, secret),
            SecretConstants.SecretTypes.Ecc => DeserializeEcc(metadata, secret),
            _ => throw new InvalidOperationException($"The '{secret.SecretType}' secret type is not supported.")
        };
    }

    private SecretKey DeserializeCertificate(KeyMetadata metadata, Secret secret)
    {
        // the certificate is owned by the secret

        switch (secret.EncodingType)
        {
            case SecretConstants.EncodingTypes.Pem:
            {
                var certificate = X509Certificate2.CreateFromPem(secret.EncodedValue);
                try
                {
                    return SecretKeyFactory.Create(metadata, certificate);
                }
                catch
                {
                    certificate.Dispose();
                    throw;
                }
            }

            case SecretConstants.EncodingTypes.Base64:
            {
                using var _ = DecodeBase64(secret.EncodedValue, out var bytes);
                var certificate = new X509Certificate2(bytes);
                try
                {
                    return SecretKeyFactory.Create(metadata, certificate);
                }
                catch
                {
                    certificate.Dispose();
                    throw;
                }
            }

            default:
                throw InvalidEncodingType(secret.EncodingType, "certificate");
        }
    }

    private SecretKey DeserializeSymmetric(KeyMetadata metadata, Secret secret)
    {
        switch (secret.EncodingType)
        {
            case SecretConstants.EncodingTypes.None:
                return SecretKeyFactory.CreateSymmetric(metadata, secret.EncodedValue);

            case SecretConstants.EncodingTypes.Base64:
            {
                using var _ = DecodeBase64(secret.EncodedValue, out var bytes);
                return SecretKeyFactory.CreateSymmetric(metadata, bytes);
            }

            default:
                throw InvalidEncodingType(secret.EncodingType, "symmetric");
        }
    }

    private SecretKey DeserializeRsa(KeyMetadata metadata, Secret secret)
    {
        switch (secret.EncodingType)
        {
            case SecretConstants.EncodingTypes.Pem:
                return SecretKeyFactory.CreateRsaPem(metadata, secret.EncodedValue);

            case SecretConstants.EncodingTypes.Base64:
            {
                using var _ = DecodeBase64(secret.EncodedValue, out var bytes);
                return SecretKeyFactory.CreateRsaPkcs8(metadata, bytes);
            }

            default:
                throw InvalidEncodingType(secret.EncodingType, "RSA");
        }
    }

    private SecretKey DeserializeEcc(KeyMetadata metadata, Secret secret)
    {
        switch (secret.EncodingType)
        {
            case SecretConstants.EncodingTypes.Pem:
                return SecretKeyFactory.CreateEccPem(metadata, secret.EncodedValue);

            case SecretConstants.EncodingTypes.Base64:
            {
                using var _ = DecodeBase64(secret.EncodedValue, out var bytes);
                return SecretKeyFactory.CreateEccPkcs8(metadata, bytes);
            }

            default:
                throw InvalidEncodingType(secret.EncodingType, "ECC");
        }
    }

    private static InvalidOperationException InvalidEncodingType(string encodingType, string keyType) =>
        new($"The '{encodingType}' encoding type is not supported when deserializing {keyType} secrets.");

    private static IDisposable DecodeBase64(ReadOnlySpan<char> chars, out Span<byte> bytes)
    {
        if (chars.Length == 0)
        {
            bytes = Span<byte>.Empty;
            return EmptyDisposable.Singleton;
        }

        var maxByteCount = Base64Url.GetByteCountForDecode(chars.Length);
        var lease = CryptoPool.Rent(maxByteCount, isSensitive: true, out bytes);
        try
        {
            var result = Convert.TryFromBase64Chars(chars, bytes, out var bytesWritten);
            Debug.Assert(result && bytesWritten > 0 && bytesWritten <= maxByteCount);
            bytes = bytes[..bytesWritten];
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }
}
