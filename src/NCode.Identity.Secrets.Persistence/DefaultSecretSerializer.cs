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
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction: a thin persistence wrapper
/// that unprotects the stored key material and delegates decoding to the <see cref="ISecretKeyTypeDescriptor"/> for
/// the secret's type (the single extensibility seam).
/// </summary>
internal class DefaultSecretSerializer(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory
) : ISecretSerializer
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
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
    public SecretKey DeserializeSecret(PersistedSecret persistedSecret)
    {
        if (!SecretKeyTypeCatalog.TryGet(persistedSecret.SecretType, out var descriptor))
        {
            throw new InvalidOperationException(
                $"The '{persistedSecret.SecretType}' secret type is not supported."
            );
        }

        var metadata = new KeyMetadata
        {
            KeyId = persistedSecret.SecretId,
            Use = persistedSecret.Use,
            Algorithm = persistedSecret.Algorithm,
            ExpiresWhen = persistedSecret.ExpiresWhen,
        };

        const bool base64UrlIsSensitive = false;
        const bool keyMaterialIsSensitive = true;

        using var protectedDataWriter = BufferFactory.CreatePooledBufferWriter(
            base64UrlIsSensitive
        );

        Base64Url.Decode(persistedSecret.EncodedValue, protectedDataWriter);

        using var protectedDataSpanLease = protectedDataWriter.GetSpanLease(base64UrlIsSensitive);

        var keySizeBytes = persistedSecret.KeySizeBits >> 3;
        using var keyMaterialBuffer = BufferFactory.CreatePooledBufferWriter(
            keyMaterialIsSensitive,
            keySizeBytes
        );
        IBufferWriter<byte> keyMaterialWriter = keyMaterialBuffer;

        DataProtector.UnprotectSpan(protectedDataSpanLease.Span, ref keyMaterialWriter);

        using var keyMaterialSpanLease = keyMaterialBuffer.GetSpanLease(keyMaterialIsSensitive);

        var secretKey = descriptor.DeserializeKeyMaterial(
            metadata,
            persistedSecret.SecretEncoding,
            keyMaterialSpanLease.Span
        );

        Debug.Assert(secretKey.KeySizeBits == persistedSecret.KeySizeBits);

        return secretKey;
    }
}
