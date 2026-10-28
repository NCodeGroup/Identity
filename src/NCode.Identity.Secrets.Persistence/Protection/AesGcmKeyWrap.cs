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

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Wraps and unwraps a key with a root key that is held in memory, using AES-GCM. The key wrappers that keep
/// their root key in the process share this implementation.
/// </summary>
/// <remarks>
/// <para>
/// A wrapped key is the base64url encoding of a random 12-byte nonce, the encrypted key, which is as long as the
/// key, and the 16-byte authentication tag. The context is the associated data, so a wrapped key can only be
/// unwrapped with the context that it was wrapped with.
/// </para>
/// <para>
/// The identifier of a root key is a fingerprint, which is the base64url encoding of the SHA-256 hash of the root
/// key. The fingerprint of a random 256-bit key reveals nothing about the key.
/// </para>
/// </remarks>
// See ADR-0235.
internal static class AesGcmKeyWrap
{
    /// <summary>
    /// The number of characters in a fingerprint, which is the base64url encoding of a SHA-256 hash.
    /// </summary>
    public const int FingerprintLength = 43;

    private const int MinimumWrappedSizeBytes =
        SecretProtectionFormat.NonceSizeBytes + SecretProtectionFormat.TagSizeBytes;

    /// <summary>
    /// Gets the number of characters that <see cref="Wrap{TWriter}"/> writes for a key of a given length.
    /// </summary>
    /// <param name="keyLength">The length of the key to wrap, in bytes.</param>
    /// <returns>The length of the wrapped key as base64url text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="keyLength"/> is negative.
    /// </exception>
    public static int GetWrappedLength(int keyLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(keyLength);

        return Base64Url.GetCharCountForEncode(MinimumWrappedSizeBytes + keyLength);
    }

    /// <summary>
    /// Wraps a key with a root key and writes the base64url text of the wrapped key to a writer.
    /// </summary>
    /// <typeparam name="TWriter">The type of the writer that receives the wrapped key.</typeparam>
    /// <param name="rootKey">The root key, which must be a valid AES key.</param>
    /// <param name="key">The key to wrap.</param>
    /// <param name="context">The associated data that is bound to the wrapped key.</param>
    /// <param name="destination">
    /// The writer that receives exactly <see cref="GetWrappedLength"/> characters for the length of
    /// <paramref name="key"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="destination"/> does not provide the requested space.
    /// </exception>
    public static void Wrap<TWriter>(
        ReadOnlySpan<byte> rootKey,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        var wrappedSize = MinimumWrappedSizeBytes + key.Length;
        using var lease = BufferFactory.Rent(
            wrappedSize,
            isSensitive: false,
            out Span<byte> wrapped
        );

        var nonce = wrapped[..SecretProtectionFormat.NonceSizeBytes];
        var ciphertext = wrapped.Slice(SecretProtectionFormat.NonceSizeBytes, key.Length);
        var tag = wrapped[^SecretProtectionFormat.TagSizeBytes..];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(rootKey, SecretProtectionFormat.TagSizeBytes);
        aes.Encrypt(nonce, key, ciphertext, tag, context);

        var chars = destination.GetSpan(GetWrappedLength(key.Length));
        if (!Base64Url.TryEncode(wrapped, chars, out var charsWritten))
        {
            throw new InvalidOperationException(
                "The destination did not provide the requested space."
            );
        }

        destination.Advance(charsWritten);
    }

    /// <summary>
    /// Unwraps a key that was wrapped by <see cref="Wrap{TWriter}"/> and writes it into a buffer that the caller
    /// provides. The caller owns the buffer and is responsible for zeroing it when the key is no longer needed.
    /// </summary>
    /// <param name="rootKey">The root key that wrapped the key.</param>
    /// <param name="ciphertext">The wrapped key as base64url text.</param>
    /// <param name="context">The associated data that was bound to the wrapped key.</param>
    /// <param name="destination">
    /// The buffer that receives the unwrapped key, which must be at least as long as the key. The bytes that were
    /// written are zeroed when the key cannot be unwrapped.
    /// </param>
    /// <returns>The length of the unwrapped key.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is too small to hold the unwrapped key.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key is not valid base64url text, is too short, or was altered, or when the root key or
    /// the context does not match the ones that wrapped the key.
    /// </exception>
    public static int Unwrap(
        ReadOnlySpan<byte> rootKey,
        ReadOnlySpan<char> ciphertext,
        ReadOnlySpan<byte> context,
        Span<byte> destination
    )
    {
        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (ciphertext.Length % 4 == 1)
        {
            throw new CryptographicException("The wrapped key is not valid base64url text.");
        }

        var wrappedSize = Base64Url.GetByteCountForDecode(ciphertext.Length);
        if (wrappedSize < MinimumWrappedSizeBytes)
        {
            throw new CryptographicException("The wrapped key is too short.");
        }

        var keyLength = wrappedSize - MinimumWrappedSizeBytes;
        if (destination.Length < keyLength)
        {
            throw new ArgumentException(
                "The destination is too small to hold the unwrapped key.",
                nameof(destination)
            );
        }

        using var lease = BufferFactory.Rent(
            wrappedSize,
            isSensitive: false,
            out Span<byte> wrapped
        );

        int bytesWritten;
        try
        {
            if (!Base64Url.TryDecode(ciphertext, wrapped, out bytesWritten))
            {
                throw new CryptographicException("The wrapped key is not valid base64url text.");
            }
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(
                "The wrapped key is not valid base64url text.",
                exception
            );
        }

        if (bytesWritten != wrappedSize)
        {
            throw new CryptographicException("The wrapped key is not valid base64url text.");
        }

        var nonce = wrapped[..SecretProtectionFormat.NonceSizeBytes];
        var encrypted = wrapped.Slice(SecretProtectionFormat.NonceSizeBytes, keyLength);
        var tag = wrapped[(SecretProtectionFormat.NonceSizeBytes + keyLength)..];

        var key = destination[..keyLength];
        var succeeded = false;
        try
        {
            using var aes = new AesGcm(rootKey, SecretProtectionFormat.TagSizeBytes);
            aes.Decrypt(nonce, encrypted, tag, key, context);

            succeeded = true;
            return keyLength;
        }
        finally
        {
            if (!succeeded)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    /// <summary>
    /// Computes the fingerprint of a root key, which is the identifier that a wrapped key records for the root key
    /// that wrapped it.
    /// </summary>
    /// <param name="rootKey">The root key.</param>
    /// <param name="destination">
    /// The buffer that receives the base64url encoding of the SHA-256 hash of the root key, which must be at least
    /// <see cref="FingerprintLength"/> characters long.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is shorter than <see cref="FingerprintLength"/>.
    /// </exception>
    public static void ComputeFingerprint(ReadOnlySpan<byte> rootKey, Span<char> destination)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(rootKey, hash);

        if (!Base64Url.TryEncode(hash, destination, out _))
        {
            throw new ArgumentException(
                "The destination is too small to hold the fingerprint.",
                nameof(destination)
            );
        }
    }
}
