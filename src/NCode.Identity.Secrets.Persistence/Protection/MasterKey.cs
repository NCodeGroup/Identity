// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Holds the unwrapped key material of a single master key together with the identity that the key is registered
/// under. The key material lives in a pinned array from the secure memory pool so that the garbage collector never
/// copies it, and it is zeroed when the master key is disposed.
/// </summary>
// See ADR-1235.
internal sealed class MasterKey : IDisposable
{
    private MasterKey(
        Guid keyId,
        SecretOwner owner,
        int version,
        IDisposable materialLease,
        Memory<byte> material
    )
    {
        KeyId = keyId;
        Owner = owner;
        Version = version;
        MaterialLease = materialLease;
        MaterialMemory = material;
    }

    private IDisposable MaterialLease { get; }

    /// <summary>
    /// Gets the key material of the master key as memory, for the key wrappers that take memory. The memory belongs to
    /// the master key and is zeroed when it is disposed, so a caller must not keep it.
    /// </summary>
    internal ReadOnlyMemory<byte> MaterialMemory { get; }

    /// <summary>
    /// Gets the identifier of the master key, which is written to every value that the master key protects.
    /// </summary>
    public Guid KeyId { get; }

    /// <summary>
    /// Gets the owner of the master key, with both the owner type and the owner identifier converted to lower case
    /// using the invariant culture.
    /// </summary>
    public SecretOwner Owner { get; }

    /// <summary>
    /// Gets the version of the master key within its owner.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the key material of the master key.
    /// </summary>
    internal ReadOnlySpan<byte> Material => MaterialMemory.Span;

    /// <summary>
    /// Creates a master key from key material, which is copied into a sensitive rental that the master key owns.
    /// </summary>
    /// <param name="keyId">The identifier of the master key.</param>
    /// <param name="owner">The owner of the master key, which is normalized.</param>
    /// <param name="version">The version of the master key within its owner.</param>
    /// <param name="material">The key material, which must be exactly 32 bytes long.</param>
    /// <returns>The new <see cref="MasterKey"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="material"/> is not exactly 32 bytes long.
    /// </exception>
    public static MasterKey Create(
        Guid keyId,
        SecretOwner owner,
        int version,
        ReadOnlySpan<byte> material
    )
    {
        if (material.Length != SecretProtectionFormat.MasterKeySizeBytes)
        {
            throw new ArgumentException(
                $"The key material must be exactly {SecretProtectionFormat.MasterKeySizeBytes} bytes long.",
                nameof(material)
            );
        }

        var normalizedOwner = owner.Normalize();

        var materialLease = BufferFactory.Rent(
            SecretProtectionFormat.MasterKeySizeBytes,
            isSensitive: true,
            out Memory<byte> materialMemory
        );

        material.CopyTo(materialMemory.Span);

        return new MasterKey(keyId, normalizedOwner, version, materialLease, materialMemory);
    }

    /// <summary>
    /// Zeroes the key material of the master key and returns it to the secure memory pool.
    /// </summary>
    public void Dispose()
    {
        MaterialLease.Dispose();
    }
}
