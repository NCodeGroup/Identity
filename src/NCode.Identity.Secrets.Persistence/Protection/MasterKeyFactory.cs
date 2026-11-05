// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
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
/// A new master key is 32 random bytes that are wrapped by the root of its owner, bound to the identifier and the
/// owner of the master key. The root of an owner is the root key the owner brings, when it has an active one, and
/// otherwise the configured root key provider, with a recovery copy under the recovery public key when one is
/// configured. A master key under an owner root key has no recovery copy. The row records a key-check value that binds
/// the identifier, the owner and the version to the key material.
/// </para>
/// <para>
/// Every temporary copy of the key material is zeroed before a method returns, and no exception message contains key
/// material.
/// </para>
/// </remarks>
/// <param name="catalog">The catalog that resolves the root key provider that wraps and unwraps master keys.</param>
/// <param name="recovery">The wrapper that wraps a new master key under the offline recovery public key.</param>
/// <param name="timeProvider">The time provider that supplies the creation time of a new master key.</param>
// See ADR-1235 and ADR-1411.
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
    /// <param name="rootKey">The root key the owner brings, or <see langword="null"/> when it has none.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The row to persist, which has no retirement time, and the active master key. The caller owns the master key and
    /// must dispose it.
    /// </returns>
    /// <exception cref="OwnerRootKeyUnavailableException">
    /// Thrown before anything is wrapped when the owner root key is unavailable.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the root key provider that wraps the key is not registered.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the key cannot be wrapped by the root key provider or by the recovery key.
    /// </exception>
    public async ValueTask<CreatedMasterKey> CreateAsync(
        SecretOwner owner,
        int version,
        PersistedOwnerRootKey? rootKey,
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

        var wraps = await WrapMaterialAsync(keyId, owner, material, rootKey, cancellationToken);

        var row = new PersistedMasterKey
        {
            KeyId = keyId,
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            Version = version,
            CreatedWhen = TimeProvider.GetUtcNow(),
            RootKeyProvider = wraps.Root.WrapperName,
            RootKeyId = wraps.Root.KeyId,
            RootKeyReference = wraps.RootKeyReference,
            RootWrappedKey = wraps.Root.Ciphertext,
            RecoveryKeyId = wraps.Recovery?.KeyId,
            RecoveryWrappedKey = wraps.Recovery?.Ciphertext,
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
    /// Wraps a master key under the root of its owner, bound to its identifier and owner: the root key the owner
    /// brings when it is active, and otherwise the configured root key provider and the recovery public key.
    /// </summary>
    /// <param name="masterKey">The master key to wrap.</param>
    /// <param name="rootKey">The root key the owner brings, or <see langword="null"/> when it has none.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The wraps, with the name of the provider and the identifier of the root key.</returns>
    /// <exception cref="OwnerRootKeyUnavailableException">
    /// Thrown before anything is wrapped when the owner root key is unavailable.
    /// </exception>
    // See ADR-1408 and ADR-1411.
    public async ValueTask<MasterKeyWraps> WrapAsync(
        MasterKey masterKey,
        PersistedOwnerRootKey? rootKey,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        return await WrapMaterialAsync(
            masterKey.KeyId,
            masterKey.Owner,
            masterKey.MaterialMemory,
            rootKey,
            cancellationToken
        );
    }

    /// <summary>
    /// Wraps a master key under the recovery public key alone, for a run that replaces only the recovery copies of
    /// master keys that the deployment's root key wraps.
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

    /// <summary>
    /// Determines whether an owner root key wraps the master keys of its owner: it exists and is not being removed.
    /// </summary>
    /// <param name="rootKey">The root key the owner brings, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> when the master keys of the owner belong under the root key.</returns>
    public static bool IsOwnerRooted([NotNullWhen(true)] PersistedOwnerRootKey? rootKey) =>
        rootKey is not null
        && !string.Equals(rootKey.State, OwnerRootKeyStates.Removing, StringComparison.Ordinal);

    /// <summary>
    /// Determines whether a persisted master key is wrapped under the root of its owner. With an owner root key that is
    /// the key itself and no recovery copy; without one, or while it is being removed, it is the deployment's root key.
    /// </summary>
    /// <param name="row">The persisted master key.</param>
    /// <param name="rootKey">The root key the owner brings, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> when the master key conforms to the root of its owner.</returns>
    public static bool Conforms(PersistedMasterKey row, PersistedOwnerRootKey? rootKey)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!IsOwnerRooted(rootKey))
            return row.RootKeyReference is null;

        return string.Equals(row.RootKeyProvider, rootKey.KeyWrapperName, StringComparison.Ordinal)
            && string.Equals(row.RootKeyReference, rootKey.KeyReference, StringComparison.Ordinal)
            && row.RecoveryKeyId is null
            && row.RecoveryWrappedKey is null;
    }

    // Wraps the material under the root of its owner: the active owner root key, with no recovery copy, or else the
    // configured root key provider, with a recovery copy when a recovery public key is configured.
    private async ValueTask<MasterKeyWraps> WrapMaterialAsync(
        Guid keyId,
        SecretOwner owner,
        ReadOnlyMemory<byte> material,
        PersistedOwnerRootKey? rootKey,
        CancellationToken cancellationToken
    )
    {
        var ownerRoot = IsOwnerRooted(rootKey) ? rootKey : null;

        // An unavailable or unknown state fails closed before any wrap is attempted.
        if (
            ownerRoot is not null
            && !string.Equals(ownerRoot.State, OwnerRootKeyStates.Active, StringComparison.Ordinal)
        )
        {
            throw new OwnerRootKeyUnavailableException(owner);
        }

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

        if (ownerRoot is not null)
        {
            var referenced = Catalog.GetReferenced(ownerRoot.KeyWrapperName);
            var wrappedUnderOwnerRoot = await referenced.WrapAsync(
                ownerRoot.KeyReference,
                material,
                context,
                cancellationToken
            );

            return new MasterKeyWraps(
                wrappedUnderOwnerRoot,
                ownerRoot.KeyReference,
                Recovery: null,
                ClearsRecovery: true
            );
        }

        var wrapped = await Catalog.GetConfigured().WrapAsync(material, context, cancellationToken);

        var recoveryWrapped = Recovery.IsConfigured
            ? await Recovery.WrapAsync(material, ReadOnlyMemory<byte>.Empty, cancellationToken)
            : null;

        return new MasterKeyWraps(
            wrapped,
            RootKeyReference: null,
            recoveryWrapped,
            ClearsRecovery: false
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
