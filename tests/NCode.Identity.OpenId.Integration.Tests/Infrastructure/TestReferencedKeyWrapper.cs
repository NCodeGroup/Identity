// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A key wrapper that stands in for a key management service a tenant controls: every key reference names its own
/// AES-256-GCM key, which a test can revoke and restore. A revoked key wraps and unwraps nothing, as a key that its
/// owner disabled or whose grant the owner withdrew.
/// </summary>
public sealed class TestReferencedKeyWrapper : IReferencedKeyWrapper
{
    /// <summary>
    /// The name that identifies this wrapper.
    /// </summary>
    public const string WrapperName = "test-tenant-kms";

    /// <summary>
    /// The prefix that every key reference starts with.
    /// </summary>
    public const string ReferencePrefix = "test-key-";

    /// <summary>
    /// The prefix that every access identity starts with.
    /// </summary>
    public const string AccessIdentityPrefix = "test-identity-";

    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private ConcurrentDictionary<string, byte[]> Keys { get; } = new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, bool> RevokedReferences { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string Name => WrapperName;

    /// <summary>
    /// Revokes a key, so that wrapping and unwrapping through it fails until it is restored.
    /// </summary>
    /// <param name="keyReference">The reference to the key.</param>
    public void Revoke(string keyReference) => RevokedReferences[keyReference] = true;

    /// <summary>
    /// Restores a revoked key, so that it wraps and unwraps again.
    /// </summary>
    /// <param name="keyReference">The reference to the key.</param>
    public void Restore(string keyReference) => RevokedReferences.TryRemove(keyReference, out _);

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyReference,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (
            string.IsNullOrEmpty(keyReference)
            || !keyReference.StartsWith(ReferencePrefix, StringComparison.Ordinal)
        )
        {
            failure = $"must start with '{ReferencePrefix}'";
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryValidateAccessIdentity(
        string accessIdentity,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (
            string.IsNullOrEmpty(accessIdentity)
            || !accessIdentity.StartsWith(AccessIdentityPrefix, StringComparison.Ordinal)
        )
        {
            failure = $"must start with '{AccessIdentityPrefix}'";
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    public ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    ) =>
        throw new NotSupportedException(
            $"The key wrapper '{WrapperName}' wraps only under a key that the caller names."
        );

    /// <inheritdoc />
    public ValueTask<WrappedKey> WrapAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(referencedKey);

        var keyReference = referencedKey.KeyReference;
        if (!TryValidateKeyReference(keyReference, out var failure))
            throw new ArgumentException($"The key reference {failure}.", nameof(referencedKey));

        if (
            referencedKey.Access is { } access
            && !TryValidateAccessIdentity(access.Identity, out var accessFailure)
        )
        {
            throw new ArgumentException(
                $"The access identity {accessFailure}.",
                nameof(referencedKey)
            );
        }

        ThrowIfRevoked(keyReference);

        var aesKey = Keys.GetOrAdd(keyReference, _ => RandomNumberGenerator.GetBytes(KeySizeBytes));

        var buffer = new byte[NonceSizeBytes + TagSizeBytes + key.Length];
        var nonce = buffer.AsSpan(0, NonceSizeBytes);
        var tag = buffer.AsSpan(NonceSizeBytes, TagSizeBytes);
        var ciphertext = buffer.AsSpan(NonceSizeBytes + TagSizeBytes);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(aesKey, TagSizeBytes);
        aes.Encrypt(nonce, key.Span, ciphertext, tag, context.Span);

        return ValueTask.FromResult(
            new WrappedKey
            {
                WrapperName = WrapperName,
                KeyId = keyReference,
                Ciphertext = Base64Url.EncodeToString(buffer),
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
        ArgumentNullException.ThrowIfNull(wrappedKey);
        ArgumentNullException.ThrowIfNull(destination);

        ThrowIfRevoked(wrappedKey.KeyId);

        if (!Keys.TryGetValue(wrappedKey.KeyId, out var aesKey))
            throw new CryptographicException($"The key '{wrappedKey.KeyId}' does not exist.");

        var buffer = new byte[WrappedKeyCiphertext.GetByteCount(wrappedKey.Ciphertext)];
        WrappedKeyCiphertext.Decode(wrappedKey.Ciphertext, buffer);
        if (buffer.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("The wrapped key is too short.");

        var nonce = buffer.AsSpan(0, NonceSizeBytes);
        var tag = buffer.AsSpan(NonceSizeBytes, TagSizeBytes);
        var ciphertext = buffer.AsSpan(NonceSizeBytes + TagSizeBytes);

        var span = destination.GetSpan(ciphertext.Length)[..ciphertext.Length];
        try
        {
            using var aes = new AesGcm(aesKey, TagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, span, context.Span);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(span);
            throw;
        }

        destination.Advance(ciphertext.Length);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        KeyAccess access,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    ) => UnwrapAsync(wrappedKey, context, destination, cancellationToken);

    private void ThrowIfRevoked(string keyReference)
    {
        if (RevokedReferences.ContainsKey(keyReference))
            throw new CryptographicException($"The key '{keyReference}' is revoked.");
    }
}
