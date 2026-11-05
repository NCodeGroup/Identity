// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.KeyWrapping;
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
// See ADR-1235.
internal class MasterKeyFactory(
    IKeyWrapperCatalog catalog,
    IRecoveryKeyWrapper recovery,
    TimeProvider timeProvider
)
{
    private IKeyWrapperCatalog Catalog { get; } = catalog;

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

        var recoveryWrapped = Recovery.IsConfigured
            ? await Recovery.WrapAsync(material, ReadOnlyMemory<byte>.Empty, cancellationToken)
            : null;

        var row = new PersistedMasterKey
        {
            KeyId = keyId,
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            Version = version,
            CreatedWhen = TimeProvider.GetUtcNow(),
            RootKeyProvider = wrapped.WrapperName,
            RootKeyId = wrapped.KeyId,
            RootWrappedKey = wrapped.Ciphertext,
            RecoveryKeyId = recoveryWrapped?.KeyId,
            RecoveryWrappedKey = recoveryWrapped?.Ciphertext,
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
        var wrappedKey = new WrappedKey
        {
            WrapperName = row.RootKeyProvider,
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

        using var destination = BufferFactory.CreatePooledBufferWriter(
            isSensitive: true,
            SecretProtectionFormat.MasterKeySizeBytes
        );

        await Catalog
            .Get(row.RootKeyProvider)
            .UnwrapAsync(wrappedKey, context, destination, cancellationToken);

        using var material = destination.GetSpanLease(isSensitive: true);

        return CreateVerifiedKey(material.Span, row, owner);
    }

    /// <summary>
    /// Unwraps a persisted master key from its recovery copy with the recovery private key, and verifies the key the
    /// way <see cref="UnwrapAsync"/> does.
    /// </summary>
    /// <param name="row">The persisted master key to unwrap; it must carry a recovery copy.</param>
    /// <param name="unwrapper">The unwrapper that holds the recovery private key.</param>
    /// <returns>The master key, which the caller owns and must dispose.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when the row has no recovery copy under the unwrapper's key, when the copy cannot be unwrapped, or when
    /// the unwrapped key does not match the size or the key-check value of the row.
    /// </exception>
    // See ADR-1408.
    public static MasterKey UnwrapWithRecovery(
        PersistedMasterKey row,
        RecoveryKeyUnwrapper unwrapper
    )
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(unwrapper);

        var owner = row.Owner;
        if (
            row.RecoveryWrappedKey is null
            || !string.Equals(row.RecoveryKeyId, unwrapper.RecoveryKeyId, StringComparison.Ordinal)
        )
        {
            throw new CryptographicException(
                $"The master key '{row.KeyId}' of owner '{owner}' has no recovery copy under the recovery key '{unwrapper.RecoveryKeyId}'."
            );
        }

        using var destination = BufferFactory.CreatePooledBufferWriter(
            isSensitive: true,
            SecretProtectionFormat.MasterKeySizeBytes
        );
        unwrapper.Unwrap(row.RecoveryWrappedKey, destination);

        using var material = destination.GetSpanLease(isSensitive: true);

        return CreateVerifiedKey(material.Span, row, owner);
    }

    /// <summary>
    /// Wraps a master key under the configured root key provider, bound to its identifier and owner.
    /// </summary>
    /// <param name="masterKey">The master key to wrap.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The wrapped key, with the name of the provider and the identifier of the root key.</returns>
    // See ADR-1408.
    public async ValueTask<WrappedKey> WrapUnderRootAsync(
        MasterKey masterKey,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        var contextSize = SecretProtectionFormat.GetRootContextSize(masterKey.Owner);
        using var contextLease = BufferFactory.Rent(
            contextSize,
            isSensitive: false,
            out Memory<byte> contextBuffer
        );
        var contextWritten = SecretProtectionFormat.WriteRootContext(
            masterKey.KeyId,
            masterKey.Owner,
            contextBuffer.Span
        );

        return await Catalog
            .GetConfigured()
            .WrapAsync(
                masterKey.MaterialMemory,
                contextBuffer[..contextWritten],
                cancellationToken
            );
    }

    /// <summary>
    /// Wraps a master key under the recovery public key.
    /// </summary>
    /// <param name="masterKey">The master key to wrap.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The wrapped key, or <see langword="null"/> when no recovery key is configured.</returns>
    // See ADR-1408.
    public async ValueTask<WrappedKey?> WrapUnderRecoveryAsync(
        MasterKey masterKey,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        if (!Recovery.IsConfigured)
            return null;

        return await Recovery.WrapAsync(
            masterKey.MaterialMemory,
            ReadOnlyMemory<byte>.Empty,
            cancellationToken
        );
    }

    // Verifies the size and the key-check value of an unwrapped key and builds the master key from it.
    private static MasterKey CreateVerifiedKey(
        ReadOnlySpan<byte> material,
        PersistedMasterKey row,
        SecretOwner owner
    )
    {
        if (material.Length != SecretProtectionFormat.MasterKeySizeBytes)
        {
            throw new CryptographicException(
                $"The master key '{row.KeyId}' of owner '{owner}' does not have the size of a master key."
            );
        }

        if (!IsKeyCheckValid(material, row, owner))
        {
            throw new CryptographicException(
                $"The master key '{row.KeyId}' of owner '{owner}' does not match its key-check value."
            );
        }

        return MasterKey.Create(row.KeyId, owner, row.Version, material);
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
