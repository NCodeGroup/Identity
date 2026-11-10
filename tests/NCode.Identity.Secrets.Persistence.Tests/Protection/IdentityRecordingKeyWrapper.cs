// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Decorates a referenced key wrapper, records the access of every unwrap and refuses every wrap and unwrap under an
/// access identity that it is told to refuse. A refused call throws <see cref="CryptographicException"/>.
/// </summary>
public sealed class IdentityRecordingKeyWrapper(IReferencedKeyWrapper inner) : IReferencedKeyWrapper
{
    private ConcurrentQueue<KeyAccess> UnwrapAccessQueue { get; } = new();

    private ConcurrentDictionary<string, bool> Refused { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the access of every unwrap that carried one, in call order, including the refused ones.
    /// </summary>
    public IReadOnlyList<KeyAccess> UnwrapAccesses => UnwrapAccessQueue.ToArray();

    /// <summary>
    /// Refuses every wrap and unwrap under an access identity.
    /// </summary>
    /// <param name="identity">The access identity to refuse.</param>
    public void Refuse(string identity) => Refused[identity] = true;

    /// <inheritdoc />
    public string Name => inner.Name;

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyReference,
        [NotNullWhen(false)] out string? failure
    ) => inner.TryValidateKeyReference(keyReference, out failure);

    /// <inheritdoc />
    public bool TryValidateAccessIdentity(
        string accessIdentity,
        [NotNullWhen(false)] out string? failure
    ) => inner.TryValidateAccessIdentity(accessIdentity, out failure);

    /// <inheritdoc />
    public ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    ) => inner.WrapAsync(key, context, cancellationToken);

    /// <inheritdoc />
    public ValueTask<WrappedKey> WrapAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        ThrowIfRefused(referencedKey.Access);

        return inner.WrapAsync(referencedKey, key, context, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    ) => inner.UnwrapAsync(wrappedKey, context, destination, cancellationToken);

    /// <inheritdoc />
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        KeyAccess access,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    )
    {
        UnwrapAccessQueue.Enqueue(access);
        ThrowIfRefused(access);

        return inner.UnwrapAsync(wrappedKey, access, context, destination, cancellationToken);
    }

    private void ThrowIfRefused(KeyAccess? access)
    {
        if (access is not null && Refused.ContainsKey(access.Identity))
        {
            throw new CryptographicException("The access identity is refused.");
        }
    }
}
