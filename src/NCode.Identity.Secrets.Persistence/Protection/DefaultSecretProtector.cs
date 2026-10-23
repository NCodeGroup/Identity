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
using System.Collections.Frozen;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretProtector"/> abstraction. Every value is encrypted
/// with its own random data key, and the data key is wrapped by a key-encryption key that is derived from the active
/// master key of the owner and the purpose of the value. A value that was protected by a master key that has since
/// been retired can still be unprotected as long as the retired master key is loaded.
/// </summary>
/// <remarks>
/// Both the wrapped data key and the encrypted payload are authenticated against the owner, the purpose and the item
/// that the value belongs to, so a value cannot be moved to another owner, purpose or item. Every failure to
/// unprotect a value throws a <see cref="CryptographicException"/> that names the owner and the master key but never
/// contains key material, plaintext or the protected value.
/// </remarks>
// See ADR-0228.
internal class DefaultSecretProtector(
    SecretOwner owner,
    MasterKey activeKey,
    FrozenDictionary<Guid, MasterKey> keys
) : ISecretProtector
{
    // The offsets of the fields of a protected value, following the master key identifier at the start.
    private const int WrapNonceOffset = SecretProtectionFormat.KeyIdSizeBytes;
    private const int WrappedKeyOffset = WrapNonceOffset + SecretProtectionFormat.NonceSizeBytes;
    private const int WrapTagOffset = WrappedKeyOffset + SecretProtectionFormat.DataKeySizeBytes;
    private const int PayloadNonceOffset = WrapTagOffset + SecretProtectionFormat.TagSizeBytes;
    private const int PayloadTagOffset = PayloadNonceOffset + SecretProtectionFormat.NonceSizeBytes;
    private const int PayloadOffset = SecretProtectionFormat.HeaderSizeBytes;

    /// <inheritdoc />
    public SecretOwner Owner { get; } = owner;

    private MasterKey ActiveKey { get; } = activeKey;

    private FrozenDictionary<Guid, MasterKey> Keys { get; } = keys;

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="plaintextLength"/> is negative.
    /// </exception>
    public int GetProtectedLength(int plaintextLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(plaintextLength);

        return SecretProtectionFormat.Prefix.Length
            + Base64Url.GetCharCountForEncode(
                SecretProtectionFormat.HeaderSizeBytes + plaintextLength
            );
    }

    /// <inheritdoc />
    public int GetMaxPlaintextLength(int protectedLength)
    {
        var encodedLength = protectedLength - SecretProtectionFormat.Prefix.Length;

        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (encodedLength <= 0 || encodedLength % 4 == 1)
        {
            return 0;
        }

        var envelopeLength = Base64Url.GetByteCountForDecode(encodedLength);
        return Math.Max(0, envelopeLength - SecretProtectionFormat.HeaderSizeBytes);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the purpose or the item identifier of <paramref name="context"/> is <see langword="null"/> or empty,
    /// which is the case for a <see langword="default"/> context.
    /// </exception>
    /// <exception cref="CryptographicException">Thrown when the value cannot be protected.</exception>
    public void Protect<TWriter>(
        ReadOnlySpan<byte> plaintext,
        SecretProtectionContext context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        ValidateContext(context);

        var keyId = ActiveKey.KeyId;

        var envelopeSize = SecretProtectionFormat.HeaderSizeBytes + plaintext.Length;
        using var envelopeLease = BufferFactory.Rent(
            envelopeSize,
            isSensitive: false,
            out Span<byte> envelope
        );

        var associatedDataSize = SecretProtectionFormat.GetAssociatedDataSize(Owner, context);
        using var associatedDataLease = BufferFactory.Rent(
            associatedDataSize,
            isSensitive: false,
            out Span<byte> associatedData
        );

        Span<byte> dataKey = stackalloc byte[SecretProtectionFormat.DataKeySizeBytes];
        using var dataKeyLifetime = dataKey.GetSecureLifetime();

        Span<byte> keyEncryptionKey = stackalloc byte[SecretProtectionFormat.DataKeySizeBytes];
        using var keyEncryptionKeyLifetime = keyEncryptionKey.GetSecureLifetime();

        try
        {
            RandomNumberGenerator.Fill(dataKey);
            SecretProtectionFormat.DeriveKeyEncryptionKey(
                ActiveKey.Material,
                keyId,
                context.Purpose,
                keyEncryptionKey
            );

            SecretProtectionFormat.WriteMasterKeyId(
                keyId,
                envelope[..SecretProtectionFormat.KeyIdSizeBytes]
            );

            SecretProtectionFormat.WriteAssociatedData(
                SecretProtectionFormat.RoleDataKeyWrap,
                keyId,
                Owner,
                context,
                associatedData
            );
            WrapDataKey(envelope, keyEncryptionKey, dataKey, associatedData);

            SecretProtectionFormat.WriteAssociatedData(
                SecretProtectionFormat.RolePayload,
                keyId,
                Owner,
                context,
                associatedData
            );
            EncryptPayload(envelope, dataKey, plaintext, associatedData);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException(
                $"The value could not be protected for owner '{Owner}' using master key '{keyId}'.",
                exception
            );
        }

        var protectedLength = GetProtectedLength(plaintext.Length);
        var chars = destination.GetSpan(protectedLength);

        SecretProtectionFormat.Prefix.CopyTo(chars);
        if (
            !Base64Url.TryEncode(
                envelope,
                chars[SecretProtectionFormat.Prefix.Length..],
                out var charsWritten
            )
        )
        {
            throw new InvalidOperationException(
                "The destination did not provide the requested space."
            );
        }

        destination.Advance(SecretProtectionFormat.Prefix.Length + charsWritten);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the purpose or the item identifier of <paramref name="context"/> is <see langword="null"/> or empty,
    /// which is the case for a <see langword="default"/> context.
    /// </exception>
    public void Unprotect<TWriter>(
        ReadOnlySpan<char> protectedValue,
        SecretProtectionContext context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        ValidateContext(context);

        var envelopeSize = GetEnvelopeSize(protectedValue);

        using var envelopeLease = BufferFactory.Rent(
            envelopeSize,
            isSensitive: false,
            out Span<byte> envelope
        );

        if (!TryDecode(protectedValue, envelope))
        {
            throw CreateUnrecognizedFormatException();
        }

        var keyId = SecretProtectionFormat.ReadMasterKeyId(envelope);
        if (!Keys.TryGetValue(keyId, out var masterKey))
        {
            throw new CryptographicException(
                $"The master key '{keyId}' is not available for owner '{Owner}'."
            );
        }

        var associatedDataSize = SecretProtectionFormat.GetAssociatedDataSize(Owner, context);
        using var associatedDataLease = BufferFactory.Rent(
            associatedDataSize,
            isSensitive: false,
            out Span<byte> associatedData
        );

        Span<byte> dataKey = stackalloc byte[SecretProtectionFormat.DataKeySizeBytes];
        using var dataKeyLifetime = dataKey.GetSecureLifetime();

        Span<byte> keyEncryptionKey = stackalloc byte[SecretProtectionFormat.DataKeySizeBytes];
        using var keyEncryptionKeyLifetime = keyEncryptionKey.GetSecureLifetime();

        try
        {
            SecretProtectionFormat.DeriveKeyEncryptionKey(
                masterKey.Material,
                keyId,
                context.Purpose,
                keyEncryptionKey
            );

            SecretProtectionFormat.WriteAssociatedData(
                SecretProtectionFormat.RoleDataKeyWrap,
                keyId,
                Owner,
                context,
                associatedData
            );
            UnwrapDataKey(envelope, keyEncryptionKey, dataKey, associatedData);

            SecretProtectionFormat.WriteAssociatedData(
                SecretProtectionFormat.RolePayload,
                keyId,
                Owner,
                context,
                associatedData
            );
            DecryptPayload(envelope, dataKey, associatedData, ref destination);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException(
                $"The protected value could not be unprotected for owner '{Owner}' using master key '{keyId}'.",
                exception
            );
        }
    }

    // A default context has a null purpose and a null item identifier, which would otherwise surface later as a
    // null reference.
    private static void ValidateContext(SecretProtectionContext context)
    {
        ArgumentException.ThrowIfNullOrEmpty(context.Purpose);
        ArgumentException.ThrowIfNullOrEmpty(context.ItemId);
    }

    private CryptographicException CreateUnrecognizedFormatException() =>
        new($"The protected value is not in a recognized format for owner '{Owner}'.");

    // Gets the size of the decoded envelope, which fails when the value is not prefixed, does not have a valid
    // base64url length, or would decode to fewer bytes than the fixed header.
    private int GetEnvelopeSize(ReadOnlySpan<char> protectedValue)
    {
        if (!protectedValue.StartsWith(SecretProtectionFormat.Prefix, StringComparison.Ordinal))
        {
            throw CreateUnrecognizedFormatException();
        }

        var encodedLength = protectedValue.Length - SecretProtectionFormat.Prefix.Length;

        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (encodedLength % 4 == 1)
        {
            throw CreateUnrecognizedFormatException();
        }

        var envelopeSize = Base64Url.GetByteCountForDecode(encodedLength);
        if (envelopeSize < SecretProtectionFormat.HeaderSizeBytes)
        {
            throw CreateUnrecognizedFormatException();
        }

        return envelopeSize;
    }

    // Decodes the base64url text that follows the prefix into the envelope, which fails when the text contains a
    // character that is not valid base64url text. Decoding such a character throws rather than returning false.
    private static bool TryDecode(ReadOnlySpan<char> protectedValue, Span<byte> envelope)
    {
        try
        {
            return Base64Url.TryDecode(
                    protectedValue[SecretProtectionFormat.Prefix.Length..],
                    envelope,
                    out var bytesWritten
                )
                && bytesWritten == envelope.Length;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Wraps the data key with the key-encryption key and writes the nonce, the wrapped key and the tag.
    private static void WrapDataKey(
        Span<byte> envelope,
        ReadOnlySpan<byte> keyEncryptionKey,
        ReadOnlySpan<byte> dataKey,
        ReadOnlySpan<byte> associatedData
    )
    {
        var nonce = envelope.Slice(WrapNonceOffset, SecretProtectionFormat.NonceSizeBytes);
        var wrappedKey = envelope.Slice(WrappedKeyOffset, SecretProtectionFormat.DataKeySizeBytes);
        var tag = envelope.Slice(WrapTagOffset, SecretProtectionFormat.TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(keyEncryptionKey, SecretProtectionFormat.TagSizeBytes);
        aes.Encrypt(nonce, dataKey, wrappedKey, tag, associatedData);
    }

    // Encrypts the payload with the data key and writes the nonce, the tag and the encrypted payload.
    private static void EncryptPayload(
        Span<byte> envelope,
        ReadOnlySpan<byte> dataKey,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData
    )
    {
        var nonce = envelope.Slice(PayloadNonceOffset, SecretProtectionFormat.NonceSizeBytes);
        var tag = envelope.Slice(PayloadTagOffset, SecretProtectionFormat.TagSizeBytes);
        var ciphertext = envelope[PayloadOffset..];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(dataKey, SecretProtectionFormat.TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
    }

    // Unwraps the data key of a decoded envelope with the key-encryption key.
    private static void UnwrapDataKey(
        ReadOnlySpan<byte> envelope,
        ReadOnlySpan<byte> keyEncryptionKey,
        Span<byte> dataKey,
        ReadOnlySpan<byte> associatedData
    )
    {
        var nonce = envelope.Slice(WrapNonceOffset, SecretProtectionFormat.NonceSizeBytes);
        var wrappedKey = envelope.Slice(WrappedKeyOffset, SecretProtectionFormat.DataKeySizeBytes);
        var tag = envelope.Slice(WrapTagOffset, SecretProtectionFormat.TagSizeBytes);

        using var aes = new AesGcm(keyEncryptionKey, SecretProtectionFormat.TagSizeBytes);
        aes.Decrypt(nonce, wrappedKey, tag, dataKey, associatedData);
    }

    // Decrypts the payload of a decoded envelope with the data key and writes the plaintext to the destination, which
    // is advanced only after the authentication tag of the payload has been verified.
    private static void DecryptPayload<TWriter>(
        scoped ReadOnlySpan<byte> envelope,
        scoped ReadOnlySpan<byte> dataKey,
        scoped ReadOnlySpan<byte> associatedData,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var nonce = envelope.Slice(PayloadNonceOffset, SecretProtectionFormat.NonceSizeBytes);
        var tag = envelope.Slice(PayloadTagOffset, SecretProtectionFormat.TagSizeBytes);
        var ciphertext = envelope[PayloadOffset..];

        var plaintext = destination.GetSpan(ciphertext.Length)[..ciphertext.Length];

        using var aes = new AesGcm(dataKey, SecretProtectionFormat.TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

        destination.Advance(ciphertext.Length);
    }
}
