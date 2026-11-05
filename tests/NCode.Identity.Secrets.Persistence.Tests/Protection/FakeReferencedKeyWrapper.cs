// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// An in-memory key wrapper that wraps under a key the caller names, with AES-GCM and one random key per reference. A
/// revoked reference fails to wrap and to unwrap. It counts its calls.
/// </summary>
public sealed class FakeReferencedKeyWrapper(string name = "fake-kms") : IReferencedKeyWrapper
{
    private ConcurrentDictionary<string, byte[]> Keys { get; } = new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, bool> Revoked { get; } = new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, bool> UnwrapRevoked { get; } = new(StringComparer.Ordinal);

    private int _wrapCount;

    private int _unwrapCount;

    /// <summary>
    /// Gets the number of wraps.
    /// </summary>
    public int WrapCount => Volatile.Read(ref _wrapCount);

    /// <summary>
    /// Gets the number of unwraps.
    /// </summary>
    public int UnwrapCount => Volatile.Read(ref _unwrapCount);

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <summary>
    /// Makes wraps and unwraps under the reference throw.
    /// </summary>
    public void Revoke(string keyReference) => Revoked[keyReference] = true;

    /// <summary>
    /// Makes only unwraps under the reference throw.
    /// </summary>
    public void RevokeUnwrapOnly(string keyReference) => UnwrapRevoked[keyReference] = true;

    /// <summary>
    /// Makes wraps and unwraps under the reference work again.
    /// </summary>
    public void Restore(string keyReference)
    {
        Revoked.TryRemove(keyReference, out _);
        UnwrapRevoked.TryRemove(keyReference, out _);
    }

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyReference,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (keyReference.StartsWith("kms://", StringComparison.Ordinal))
        {
            failure = null;
            return true;
        }

        failure = "must start with kms://";
        return false;
    }

    /// <inheritdoc />
    public ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("The fake wrapper has no configured key.");

    /// <inheritdoc />
    public ValueTask<WrappedKey> WrapAsync(
        string keyReference,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _wrapCount);
        ThrowIfRevoked(keyReference);

        var kek = Keys.GetOrAdd(keyReference, static _ => RandomNumberGenerator.GetBytes(32));
        var ciphertext = string.Create(
            AesGcmKeyWrap.GetWrappedLength(key.Length),
            (Kek: kek, Key: key, Context: context),
            static (chars, state) =>
            {
                var writer = chars.GetFixedBufferWriter();
                AesGcmKeyWrap.Wrap(state.Kek, state.Key.Span, state.Context.Span, ref writer);
            }
        );

        return ValueTask.FromResult(
            new WrappedKey
            {
                WrapperName = Name,
                KeyId = keyReference + "/v1",
                Ciphertext = ciphertext,
            }
        );
    }

    /// <inheritdoc />
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _unwrapCount);

        var keyReference = wrappedKey.KeyId[..wrappedKey.KeyId.LastIndexOf('/')];
        ThrowIfRevoked(keyReference);
        if (UnwrapRevoked.ContainsKey(keyReference))
            throw new InvalidOperationException("revoked");

        if (!Keys.TryGetValue(keyReference, out var kek))
            throw new CryptographicException("The key is unknown.");

        AesGcmKeyWrap.Unwrap(kek, wrappedKey.Ciphertext, context.Span, ref destination);

        return ValueTask.CompletedTask;
    }

    private void ThrowIfRevoked(string keyReference)
    {
        if (Revoked.ContainsKey(keyReference))
            throw new InvalidOperationException("revoked");
    }
}
