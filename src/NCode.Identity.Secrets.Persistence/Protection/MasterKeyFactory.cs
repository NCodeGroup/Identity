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

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Creates a new master key together with the row that persists it, and unwraps a persisted master key into the key
/// that protects and unprotects values.
/// </summary>
/// <remarks>
/// <para>
/// A new master key is 32 random bytes that are wrapped by the configured root key provider, bound to the identifier
/// and the owner of the master key, and, when a recovery key is configured, also wrapped under the recovery public
/// key. The row records a key-check value that binds the identifier, the owner and the version to the key material.
/// </para>
/// <para>
/// Every temporary copy of the key material is zeroed before a method returns, and no exception message contains key
/// material.
/// </para>
/// </remarks>
/// <param name="catalog">The catalog that resolves the root key provider that wraps and unwraps master keys.</param>
/// <param name="recovery">The wrapper that wraps a new master key under the offline recovery public key.</param>
/// <param name="timeProvider">The time provider that supplies the creation time of a new master key.</param>
// See ADR-0235.
internal class MasterKeyFactory(
    IRootKeyProviderCatalog catalog,
    IRecoveryKeyWrapper recovery,
    TimeProvider timeProvider
)
{
    private IRootKeyProviderCatalog Catalog { get; } = catalog;

    private IRecoveryKeyWrapper Recovery { get; } = recovery;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>
    /// Creates a new active master key for an owner.
    /// </summary>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="version">The version of the master key within its owner.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The row to persist, which has no retirement time, and the active master key. The caller owns the master key and
    /// must dispose it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured root key provider is not registered.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the key cannot be wrapped by the root key provider or by the recovery key.
    /// </exception>
    public async ValueTask<CreatedMasterKey> CreateAsync(
        SecretOwner owner,
        int version,
        CancellationToken cancellationToken
    )
    {
        var keyId = Guid.NewGuid();

        using var materialLease = BufferFactory.Rent(
            SecretProtectionFormat.MasterKeySizeBytes,
            isSensitive: true,
            out Memory<byte> material
        );
        RandomNumberGenerator.Fill(material.Span);

        var contextSize = SecretProtectionFormat.GetRootContextSize(owner);
        using var contextLease = BufferFactory.Rent(
            contextSize,
            isSensitive: false,
            out Memory<byte> contextBuffer
        );
        var contextWritten = SecretProtectionFormat.WriteRootContext(
            keyId,
            owner,
            contextBuffer.Span
        );
        var context = contextBuffer[..contextWritten];

        var wrapped = await Catalog.GetConfigured().WrapAsync(material, context, cancellationToken);

        var row = new PersistedMasterKey
        {
            KeyId = keyId,
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            Version = version,
            CreatedWhen = TimeProvider.GetUtcNow(),
            RootKeyProvider = wrapped.ProviderName,
            RootKeyId = wrapped.KeyId,
            RootWrappedKey = wrapped.Ciphertext,
            RecoveryKeyId = Recovery.RecoveryKeyId,
            RecoveryWrappedKey = CreateRecoveryWrappedKey(material),
            KeyCheck = CreateKeyCheck(material.Span, keyId, owner, version),
        };

        var key = MasterKey.Create(keyId, owner, version, material.Span);
        return new CreatedMasterKey(row, key);
    }

    /// <summary>
    /// Unwraps a persisted master key with the root key provider that wrapped it, and verifies that the key is the
    /// one that was created for the identifier, the owner and the version of the row.
    /// </summary>
    /// <param name="row">The persisted master key to unwrap.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The master key, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="row"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the root key provider that wrapped the master key is not registered.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the root key provider cannot unwrap the master key, when the unwrapped key does not have the size of
    /// a master key, or when the key-check value of the row does not match the unwrapped key.
    /// </exception>
    public async ValueTask<MasterKey> UnwrapAsync(
        PersistedMasterKey row,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(row);

        var owner = row.Owner;
        var wrappedKey = new RootWrappedKey
        {
            ProviderName = row.RootKeyProvider,
            KeyId = row.RootKeyId,
            Ciphertext = row.RootWrappedKey,
        };

        var contextSize = SecretProtectionFormat.GetRootContextSize(owner);
        using var contextLease = BufferFactory.Rent(
            contextSize,
            isSensitive: false,
            out Memory<byte> contextBuffer
        );
        var contextWritten = SecretProtectionFormat.WriteRootContext(
            row.KeyId,
            owner,
            contextBuffer.Span
        );
        var context = contextBuffer[..contextWritten];

        // The destination is twice the size of a master key, so a provider that unwraps a longer key writes it and the
        // length check below reports it, instead of the provider rejecting a destination that is too small.
        const int destinationSize = SecretProtectionFormat.MasterKeySizeBytes * 2;
        using var lease = BufferFactory.Rent(
            destinationSize,
            isSensitive: true,
            out Memory<byte> destination
        );

        var written = await Catalog
            .Get(row.RootKeyProvider)
            .UnwrapAsync(wrappedKey, context, destination, cancellationToken);

        if (written != SecretProtectionFormat.MasterKeySizeBytes)
        {
            throw new CryptographicException(
                $"The master key '{row.KeyId}' of owner '{owner}' does not have the size of a master key."
            );
        }

        var material = destination[..written];

        if (!IsKeyCheckValid(material.Span, row, owner))
        {
            throw new CryptographicException(
                $"The master key '{row.KeyId}' of owner '{owner}' does not match its key-check value."
            );
        }

        return MasterKey.Create(row.KeyId, owner, row.Version, material.Span);
    }

    // Wraps the master key under the recovery key into a string, which is persisted in a text column, or returns null
    // when no recovery key is configured.
    private string? CreateRecoveryWrappedKey(ReadOnlyMemory<byte> material)
    {
        var wrappedLength = Recovery.GetWrappedLength();
        if (wrappedLength == 0)
        {
            return null;
        }

        return string.Create(
            wrappedLength,
            (Recovery: Recovery, Material: material),
            static (chars, state) =>
            {
                var writer = chars.GetFixedBufferWriter();
                state.Recovery.Wrap(state.Material.Span, ref writer);
                if (writer.WrittenCount != chars.Length)
                {
                    throw new InvalidOperationException(
                        $"The recovery key wrapper wrote {writer.WrittenCount} characters instead of the {chars.Length} that GetWrappedLength reported."
                    );
                }
            }
        );
    }

    // Computes the key-check value of the master key as the string that the row persists.
    private static string CreateKeyCheck(
        ReadOnlySpan<byte> material,
        Guid keyId,
        SecretOwner owner,
        int version
    )
    {
        Span<byte> keyCheck = stackalloc byte[SecretProtectionFormat.KeyCheckSizeBytes];
        SecretProtectionFormat.ComputeKeyCheck(material, keyId, owner, version, keyCheck);

        return Base64Url.Encode(keyCheck);
    }

    // Determines whether the key-check value of the row is the one that the unwrapped master key produces.
    private static bool IsKeyCheckValid(
        ReadOnlySpan<byte> material,
        PersistedMasterKey row,
        SecretOwner owner
    )
    {
        var persisted = row.KeyCheck;

        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (
            persisted.Length % 4 == 1
            || Base64Url.GetByteCountForDecode(persisted.Length)
                != SecretProtectionFormat.KeyCheckSizeBytes
        )
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[SecretProtectionFormat.KeyCheckSizeBytes];
        try
        {
            if (
                !Base64Url.TryDecode(persisted, actual, out var bytesWritten)
                || bytesWritten != SecretProtectionFormat.KeyCheckSizeBytes
            )
            {
                return false;
            }
        }
        catch (FormatException)
        {
            // The persisted key-check value contains a character that is not valid base64url text.
            return false;
        }

        Span<byte> expected = stackalloc byte[SecretProtectionFormat.KeyCheckSizeBytes];
        SecretProtectionFormat.ComputeKeyCheck(material, row.KeyId, owner, row.Version, expected);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
