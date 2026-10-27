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
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction: a thin persistence wrapper
/// that unprotects the stored key material and delegates decoding to the <see cref="ISecretKeyTypeDescriptor"/> for
/// the secret's type (the single extensibility seam).
/// </summary>
// See ADR-0235.
internal class DefaultSecretSerializer(ISecretKeyTypeCatalog secretKeyTypeCatalog)
    : ISecretSerializer
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public IReadOnlyCollection<SecretKey> DeserializeSecrets(
        ISecretProtector protector,
        IReadOnlyCollection<PersistedSecret> persistedSecrets
    )
    {
        ArgumentNullException.ThrowIfNull(protector);

        return DeserializeSecretsCore(protector, persistedSecrets);
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(ISecretProtector protector, PersistedSecret persistedSecret)
    {
        ArgumentNullException.ThrowIfNull(protector);

        return DeserializeSecretCore(protector, persistedSecret, readStoredPublicKey: false);
    }

    /// <inheritdoc />
    // See ADR-0268.
    public SecretKey DeserializePublicSecret(PersistedSecret persistedSecret)
    {
        ArgumentNullException.ThrowIfNull(persistedSecret);

        if (SecretEncodings.IsPublic(persistedSecret.SecretEncoding))
        {
            return DeserializeSecretCore(null, persistedSecret, readStoredPublicKey: false);
        }

        if (persistedSecret.PublicKey is null)
        {
            throw new InvalidOperationException(
                $"The secret '{persistedSecret.SecretId}' holds '{persistedSecret.SecretEncoding}' key material and no stored public key."
            );
        }

        return DeserializeSecretCore(null, persistedSecret, readStoredPublicKey: true);
    }

    private SortedSet<SecretKey> DeserializeSecretsCore(
        ISecretProtector? protector,
        IEnumerable<PersistedSecret> persistedSecrets
    )
    {
        var secretKeys = new SortedSet<SecretKey>(SecretKeyExpiresWhenComparer.Singleton);

        foreach (var persistedSecret in persistedSecrets)
        {
            secretKeys.Add(
                DeserializeSecretCore(protector, persistedSecret, readStoredPublicKey: false)
            );
        }

        return secretKeys;
    }

    private SecretKey DeserializeSecretCore(
        ISecretProtector? protector,
        PersistedSecret persistedSecret,
        bool readStoredPublicKey
    )
    {
        if (!SecretKeyTypeCatalog.TryGet(persistedSecret.SecretType, out var descriptor))
        {
            throw new InvalidOperationException(
                $"The '{persistedSecret.SecretType}' secret type is not supported."
            );
        }

        var metadata = new KeyMetadata
        {
            KeyId = persistedSecret.KeyId ?? persistedSecret.SecretId,
            Use = persistedSecret.Use,
            Algorithm = persistedSecret.Algorithm,
            ExpiresWhen = persistedSecret.ExpiresWhen,
        };

        const bool keyMaterialIsSensitive = true;

        var keySizeBytes = persistedSecret.KeySizeBits >> 3;
        using var keyMaterialBuffer = BufferFactory.CreatePooledBufferWriter(
            keyMaterialIsSensitive,
            keySizeBytes
        );
        IBufferWriter<byte> keyMaterialWriter = keyMaterialBuffer;

        if (readStoredPublicKey)
        {
            var storedPublicKey =
                persistedSecret.PublicKey
                ?? throw new InvalidOperationException(
                    $"The secret '{persistedSecret.SecretId}' has no stored public key."
                );
            Base64Url.Decode(storedPublicKey, keyMaterialWriter);
        }
        else if (SecretEncodings.IsPublic(persistedSecret.SecretEncoding))
        {
            ReadPublicKeyMaterial(persistedSecret, keyMaterialWriter);
        }
        else
        {
            ReadPrivateKeyMaterial(protector, persistedSecret, keyMaterialWriter);
        }

        using var keyMaterialSpanLease = keyMaterialBuffer.GetSpanLease(keyMaterialIsSensitive);

        var secretKey = descriptor.DeserializeKeyMaterial(
            metadata,
            readStoredPublicKey ? SecretEncodings.Spki : persistedSecret.SecretEncoding,
            keyMaterialSpanLease.Span
        );

        Debug.Assert(secretKey.KeySizeBits == persistedSecret.KeySizeBits);

        return secretKey;
    }

    // Public key material is stored as plain base64url text.
    private static void ReadPublicKeyMaterial(
        PersistedSecret persistedSecret,
        IBufferWriter<byte> destination
    ) => Base64Url.Decode(persistedSecret.EncodedValue, destination);

    // Private and symmetric key material is stored as an envelope that is protected by the master key of its owner.
    private static void ReadPrivateKeyMaterial(
        ISecretProtector? protector,
        PersistedSecret persistedSecret,
        IBufferWriter<byte> destination
    )
    {
        var encodedValue = persistedSecret.EncodedValue;

        if (!encodedValue.StartsWith(SecretProtectionFormat.Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                $"The secret '{persistedSecret.SecretId}' is not a protected envelope."
            );
        }

        if (protector is null)
        {
            throw new CryptographicException(
                $"The secret '{persistedSecret.SecretId}' is protected under a master key and needs a protector to be read."
            );
        }

        protector.Unprotect(
            encodedValue,
            SecretProtectionContext.ForPersistedSecret(persistedSecret.SecretId),
            ref destination
        );
    }
}
