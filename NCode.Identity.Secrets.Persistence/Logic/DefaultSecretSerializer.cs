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

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction.
/// </summary>
public class DefaultSecretSerializer(
    ISecretKeyFactory secretKeyFactory,
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory
) : ISecretSerializer
{
    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;
    private IDataProtector DataProtector { get; } = dataProtectorFactory.CreateDataProtector();

    /// <inheritdoc />
    public IReadOnlyCollection<SecretKey> DeserializeSecrets(
        IEnumerable<PersistedSecret> persistedSecrets
    )
    {
        var secretKeys = new SortedSet<SecretKey>(SecretKeyExpiresWhenComparer.Singleton);

        foreach (var persistedSecret in persistedSecrets)
        {
            secretKeys.Add(DeserializeSecret(persistedSecret));
        }

        return secretKeys;
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(PersistedSecret persistedSecret) =>
        persistedSecret.SecretType switch
        {
            SecretTypes.Certificate => DeserializeCertificate(persistedSecret),
            SecretTypes.Symmetric => DeserializeSymmetric(persistedSecret),
            SecretTypes.Rsa => DeserializeRsa(persistedSecret),
            SecretTypes.Ecc => DeserializeEcc(persistedSecret),
            _ => throw new InvalidOperationException($"The '{persistedSecret.SecretType}' secret type is not supported.")
        };

    private AsymmetricSecretKey DeserializeCertificate(PersistedSecret persistedSecret) =>
        CreateSecretKey(persistedSecret, CreateUsingCertificate);

    private AsymmetricSecretKey CreateUsingCertificate(KeyMetadata metadata, ReadOnlySpan<byte> privateKeyBytes)
    {
        using var certificate = X509CertificateLoader.LoadCertificate(privateKeyBytes);
        return SecretKeyFactory.Create(metadata, certificate);
    }

    private SymmetricSecretKey DeserializeSymmetric(PersistedSecret persistedSecret) =>
        CreateSecretKey(persistedSecret, CreateUsingSymmetric);

    private SymmetricSecretKey CreateUsingSymmetric(KeyMetadata metadata, ReadOnlySpan<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateSymmetric(metadata, privateKeyBytes);

    private RsaSecretKey DeserializeRsa(PersistedSecret persistedSecret) =>
        CreateSecretKey(persistedSecret, CreateUsingRsa);

    private RsaSecretKey CreateUsingRsa(KeyMetadata metadata, ReadOnlySpan<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateRsaPkcs8(metadata, privateKeyBytes);

    private EccSecretKey DeserializeEcc(PersistedSecret persistedSecret) =>
        CreateSecretKey(persistedSecret, CreateUsingEcc);

    private EccSecretKey CreateUsingEcc(KeyMetadata metadata, ReadOnlySpan<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateEccPkcs8(metadata, privateKeyBytes);

    private T CreateSecretKey<T>(
        PersistedSecret persistedSecret,
        Func<KeyMetadata, ReadOnlySpan<byte>, T> factory
    )
        where T : SecretKey
    {
        var metadata = new KeyMetadata
        {
            KeyId = persistedSecret.SecretId,
            Use = persistedSecret.Use,
            Algorithm = persistedSecret.Algorithm,
            ExpiresWhen = persistedSecret.ExpiresWhen
        };

        const bool base64UrlIsSensitive = false;
        const bool privateKeyIsSensitive = true;

        using var protectedDataWriter = BufferFactory.CreatePooledBufferWriter(base64UrlIsSensitive);

        Base64Url.Decode(persistedSecret.EncodedValue, protectedDataWriter);

        using var protectedDataSpanLease = protectedDataWriter.GetSpanLease(base64UrlIsSensitive);

        var keySizeBytes = persistedSecret.KeySizeBits >> 3;
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(privateKeyIsSensitive, keySizeBytes);
        IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

        DataProtector.UnprotectSpan(protectedDataSpanLease.Span, ref privateKeyWriter);

        using var privateKeySpanLease = privateKeyBuffer.GetSpanLease(privateKeyIsSensitive);

        var secretKey = factory(metadata, privateKeySpanLease.Span);

        Debug.Assert(secretKey.KeySizeBits == persistedSecret.KeySizeBits);

        return secretKey;
    }
}
