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

using System.Buffers.Binary;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Defines the layout of a protected value and builds the byte strings that bind a protected value to its master key,
/// its owner, its purpose and its item.
/// </summary>
/// <remarks>
/// <para>
/// A protected value is the <see cref="Prefix"/> followed by the base64url encoding of the master key identifier
/// (<see cref="KeyIdSizeBytes"/> bytes), the nonce used to wrap the data key (<see cref="NonceSizeBytes"/> bytes),
/// the wrapped data key (<see cref="DataKeySizeBytes"/> bytes), the authentication tag of the wrapped data key
/// (<see cref="TagSizeBytes"/> bytes), the nonce used to encrypt the payload (<see cref="NonceSizeBytes"/> bytes),
/// the authentication tag of the payload (<see cref="TagSizeBytes"/> bytes), and finally the encrypted payload.
/// </para>
/// <para>
/// Every integer is written in big-endian order, every GUID is written as 16 big-endian bytes, and every string is
/// written as a 4-byte length followed by its UTF-8 bytes. The owner type, the owner identifier and the item
/// identifier are converted to lower case using the invariant culture wherever they are written; the purpose is
/// written exactly as given.
/// </para>
/// </remarks>
// See ADR-0235.
internal static class SecretProtectionFormat
{
    /// <summary>
    /// The text that begins every protected value.
    /// </summary>
    public const string Prefix = "nce1.";

    /// <summary>
    /// The size in bytes of a master key.
    /// </summary>
    public const int MasterKeySizeBytes = 32;

    /// <summary>
    /// The size in bytes of a data key and of a key-encryption key.
    /// </summary>
    public const int DataKeySizeBytes = 32;

    /// <summary>
    /// The size in bytes of an AES-GCM nonce.
    /// </summary>
    public const int NonceSizeBytes = 12;

    /// <summary>
    /// The size in bytes of an AES-GCM authentication tag.
    /// </summary>
    public const int TagSizeBytes = 16;

    /// <summary>
    /// The size in bytes of a master key identifier.
    /// </summary>
    public const int KeyIdSizeBytes = 16;

    /// <summary>
    /// The size in bytes of the fixed part of a protected value that precedes the encrypted payload: the master key
    /// identifier, the nonce, the wrapped data key and the tag of the data-key wrap, and the nonce and the tag of the
    /// payload.
    /// </summary>
    public const int HeaderSizeBytes = 104;

    /// <summary>
    /// The size in bytes of a key-check value.
    /// </summary>
    public const int KeyCheckSizeBytes = 32;

    /// <summary>
    /// The role of the associated data that authenticates the wrapped data key.
    /// </summary>
    public const byte RoleDataKeyWrap = 1;

    /// <summary>
    /// The role of the associated data that authenticates the encrypted payload.
    /// </summary>
    public const byte RolePayload = 2;

    // The header is read from the first 24 base64url characters, which are whole 4-character groups that decode to
    // 18 bytes and so cover the master key identifier.
    private const int KeyIdHeaderCharCount = 24;
    private const int KeyIdHeaderByteCount = 18;

    private static ReadOnlySpan<byte> AssociatedDataLabel =>
        "ncode-identity/secret-protection/v1"u8;

    private static ReadOnlySpan<byte> RootContextLabel =>
        "ncode-identity/secret-protection/v1/root"u8;

    private static ReadOnlySpan<byte> KeyCheckLabel =>
        "ncode-identity/secret-protection/v1/key-check"u8;

    private static ReadOnlySpan<byte> KeyEncryptionKeyLabel =>
        "ncode-identity/secret-protection/v1/kek"u8;

    /// <summary>
    /// Gets the number of bytes that <see cref="WriteAssociatedData"/> writes for an owner and a context.
    /// </summary>
    /// <remarks>
    /// The size is the same for both roles of associated data.
    /// </remarks>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="context">The purpose of the protected value and the item that the value belongs to.</param>
    /// <returns>The size of the associated data in bytes.</returns>
    public static int GetAssociatedDataSize(SecretOwner owner, SecretProtectionContext context) =>
        AssociatedDataLabel.Length
        + 1
        + 1
        + KeyIdSizeBytes
        + GetLowerInvariantLengthPrefixedSize(owner.OwnerType)
        + GetLowerInvariantLengthPrefixedSize(owner.OwnerId)
        + GetLengthPrefixedSize(context.Purpose)
        + GetLowerInvariantLengthPrefixedSize(context.ItemId);

    /// <summary>
    /// Writes the associated data that authenticates one of the two encrypted parts of a protected value.
    /// </summary>
    /// <remarks>
    /// The associated data of the payload does not contain the master key identifier, which is written as 16 zero
    /// bytes instead, so the payload is bound to its owner, its purpose and its item but not to a particular master
    /// key. The associated data of the wrapped data key contains the master key identifier.
    /// </remarks>
    /// <param name="role">
    /// The part that the associated data authenticates, either <see cref="RoleDataKeyWrap"/> or
    /// <see cref="RolePayload"/>.
    /// </param>
    /// <param name="masterKeyId">The identifier of the master key that wraps the data key.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="context">The purpose of the protected value and the item that the value belongs to.</param>
    /// <param name="destination">
    /// The buffer that receives the associated data, which must hold at least
    /// <see cref="GetAssociatedDataSize"/> bytes.
    /// </param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is too small to hold the associated data.
    /// </exception>
    public static int WriteAssociatedData(
        byte role,
        Guid masterKeyId,
        SecretOwner owner,
        SecretProtectionContext context,
        Span<byte> destination
    )
    {
        EnsureCapacity(destination, AssociatedDataLabel.Length + 1 + 1 + KeyIdSizeBytes);

        var offset = WriteLabel(destination, AssociatedDataLabel);

        destination[offset] = role;
        offset += 1;

        // The payload is not bound to a master key, so its key identifier is 16 zero bytes.
        var keyIdField = destination.Slice(offset, KeyIdSizeBytes);
        if (role == RolePayload)
        {
            keyIdField.Clear();
        }
        else
        {
            WriteMasterKeyId(masterKeyId, keyIdField);
        }

        offset += KeyIdSizeBytes;

        offset += WriteLowerInvariantLengthPrefixed(destination[offset..], owner.OwnerType);
        offset += WriteLowerInvariantLengthPrefixed(destination[offset..], owner.OwnerId);
        offset += WriteLengthPrefixed(destination[offset..], context.Purpose);
        offset += WriteLowerInvariantLengthPrefixed(destination[offset..], context.ItemId);

        return offset;
    }

    /// <summary>
    /// Derives the key-encryption key for a purpose from a master key.
    /// </summary>
    /// <remarks>
    /// The key-encryption key is derived using HKDF with SHA-256. The master key is the input keying material, the
    /// master key identifier is the salt, and a label followed by the purpose is the context information. The length
    /// of <paramref name="destination"/> is the length of the derived key.
    /// </remarks>
    /// <param name="masterKey">The master key.</param>
    /// <param name="masterKeyId">The identifier of the master key.</param>
    /// <param name="purpose">The purpose of the protected values that the key-encryption key wraps keys for.</param>
    /// <param name="destination">The buffer that receives the derived key.</param>
    public static void DeriveKeyEncryptionKey(
        ReadOnlySpan<byte> masterKey,
        Guid masterKeyId,
        string purpose,
        Span<byte> destination
    )
    {
        Span<byte> salt = stackalloc byte[KeyIdSizeBytes];
        WriteMasterKeyId(masterKeyId, salt);

        var infoSize = KeyEncryptionKeyLabel.Length + 1 + SecureEncoding.UTF8.GetByteCount(purpose);
        using var lease = BufferFactory.Rent(infoSize, isSensitive: false, out Span<byte> info);
        var offset = WriteLabel(info, KeyEncryptionKeyLabel);
        SecureEncoding.UTF8.GetBytes(purpose, info[offset..]);

        HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, destination, salt, info);
    }

    /// <summary>
    /// Computes the key-check value of a master key into a buffer, which proves that the master key that was unwrapped
    /// is the one that was created for the given identifier, owner and version.
    /// </summary>
    /// <remarks>
    /// The key-check value is the HMAC-SHA256 of a label, the master key identifier, the owner and the version, keyed
    /// with the master key.
    /// </remarks>
    /// <param name="masterKey">The master key.</param>
    /// <param name="masterKeyId">The identifier of the master key.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="version">The version of the master key within its owner.</param>
    /// <param name="destination">
    /// The buffer that receives the key-check value, which must hold at least <see cref="KeyCheckSizeBytes"/> bytes.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is shorter than <see cref="KeyCheckSizeBytes"/> bytes.
    /// </exception>
    public static void ComputeKeyCheck(
        ReadOnlySpan<byte> masterKey,
        Guid masterKeyId,
        SecretOwner owner,
        int version,
        Span<byte> destination
    )
    {
        EnsureCapacity(destination, KeyCheckSizeBytes);

        var dataSize = GetOwnerBoundSize(KeyCheckLabel, owner) + sizeof(int);
        using var lease = BufferFactory.Rent(dataSize, isSensitive: false, out Span<byte> data);

        var offset = WriteOwnerBound(KeyCheckLabel, masterKeyId, owner, data);
        BinaryPrimitives.WriteInt32BigEndian(data[offset..], version);

        HMACSHA256.HashData(masterKey, data, destination);
    }

    /// <summary>
    /// Gets the number of bytes that <see cref="WriteRootContext"/> writes for an owner.
    /// </summary>
    /// <param name="owner">The owner of the master key.</param>
    /// <returns>The size of the context in bytes.</returns>
    public static int GetRootContextSize(SecretOwner owner) =>
        GetOwnerBoundSize(RootContextLabel, owner);

    /// <summary>
    /// Writes the context that binds a wrapped master key to its identifier and its owner.
    /// </summary>
    /// <param name="masterKeyId">The identifier of the master key.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="destination">
    /// The buffer that receives the context, which must hold at least <see cref="GetRootContextSize"/> bytes.
    /// </param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is too small to hold the context.
    /// </exception>
    public static int WriteRootContext(
        Guid masterKeyId,
        SecretOwner owner,
        Span<byte> destination
    ) => WriteOwnerBound(RootContextLabel, masterKeyId, owner, destination);

    /// <summary>
    /// Attempts to read the identifier of the master key from a protected value without decrypting anything.
    /// </summary>
    /// <remarks>
    /// Only the header of the protected value is decoded, so the encrypted payload is not validated here.
    /// </remarks>
    /// <param name="protectedValue">The protected value.</param>
    /// <param name="masterKeyId">
    /// When this method returns <see langword="true"/>, the identifier of the master key that wrapped the data key of
    /// the protected value; otherwise <see cref="Guid.Empty"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the value starts with <see cref="Prefix"/>, the remainder has a valid base64url
    /// length that decodes to at least <see cref="HeaderSizeBytes"/> bytes, and the header is valid base64url text;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryReadMasterKeyId(ReadOnlySpan<char> protectedValue, out Guid masterKeyId)
    {
        masterKeyId = Guid.Empty;

        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var encoded = protectedValue[Prefix.Length..];

        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (encoded.Length % 4 == 1)
        {
            return false;
        }

        if (Base64Url.GetByteCountForDecode(encoded.Length) < HeaderSizeBytes)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[KeyIdHeaderByteCount];

        try
        {
            if (!Base64Url.TryDecode(encoded[..KeyIdHeaderCharCount], header, out var bytesWritten))
            {
                return false;
            }

            if (bytesWritten < KeyIdSizeBytes)
            {
                return false;
            }
        }
        catch (FormatException)
        {
            // The header contains a character that is not valid base64url text.
            return false;
        }

        masterKeyId = ReadMasterKeyId(header);
        return true;
    }

    /// <summary>
    /// Writes a master key identifier as 16 big-endian bytes.
    /// </summary>
    /// <param name="masterKeyId">The identifier of the master key.</param>
    /// <param name="destination">The buffer that receives the bytes, which must hold at least 16 bytes.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is shorter than <see cref="KeyIdSizeBytes"/> bytes.
    /// </exception>
    public static void WriteMasterKeyId(Guid masterKeyId, Span<byte> destination)
    {
        if (!masterKeyId.TryWriteBytes(destination, bigEndian: true, out _))
        {
            throw new ArgumentException(
                $"The destination must hold at least {KeyIdSizeBytes} bytes.",
                nameof(destination)
            );
        }
    }

    /// <summary>
    /// Reads a master key identifier from 16 big-endian bytes.
    /// </summary>
    /// <param name="source">The bytes to read, which must hold at least 16 bytes.</param>
    /// <returns>The identifier of the master key.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="source"/> is shorter than <see cref="KeyIdSizeBytes"/> bytes.
    /// </exception>
    public static Guid ReadMasterKeyId(ReadOnlySpan<byte> source) =>
        new(source[..KeyIdSizeBytes], bigEndian: true);

    // Writes the label followed by a zero byte and returns the number of bytes written.
    private static int WriteLabel(Span<byte> destination, ReadOnlySpan<byte> label)
    {
        label.CopyTo(destination);
        destination[label.Length] = 0;
        return label.Length + 1;
    }

    private static int GetOwnerBoundSize(ReadOnlySpan<byte> label, SecretOwner owner) =>
        label.Length
        + 1
        + KeyIdSizeBytes
        + GetLowerInvariantLengthPrefixedSize(owner.OwnerType)
        + GetLowerInvariantLengthPrefixedSize(owner.OwnerId);

    // Writes the shared layout of the root context and the key-check value into the destination and returns the
    // number of bytes written.
    private static int WriteOwnerBound(
        ReadOnlySpan<byte> label,
        Guid masterKeyId,
        SecretOwner owner,
        Span<byte> destination
    )
    {
        EnsureCapacity(destination, label.Length + 1 + KeyIdSizeBytes);

        var offset = WriteLabel(destination, label);

        WriteMasterKeyId(masterKeyId, destination.Slice(offset, KeyIdSizeBytes));
        offset += KeyIdSizeBytes;

        offset += WriteLowerInvariantLengthPrefixed(destination[offset..], owner.OwnerType);
        offset += WriteLowerInvariantLengthPrefixed(destination[offset..], owner.OwnerId);

        return offset;
    }

    private static void EnsureCapacity(Span<byte> destination, int requiredSize)
    {
        if (destination.Length < requiredSize)
        {
            throw new ArgumentException(
                $"The destination must hold at least {requiredSize} bytes.",
                nameof(destination)
            );
        }
    }

    private static int GetLengthPrefixedSize(ReadOnlySpan<char> value) =>
        sizeof(int) + SecureEncoding.UTF8.GetByteCount(value);

    // Writes the 4-byte big-endian length of the UTF-8 bytes of the value followed by those bytes, and returns the
    // number of bytes written.
    private static int WriteLengthPrefixed(Span<byte> destination, ReadOnlySpan<char> value)
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(value);
        EnsureCapacity(destination, sizeof(int) + byteCount);

        BinaryPrimitives.WriteInt32BigEndian(destination, byteCount);
        SecureEncoding.UTF8.GetBytes(value, destination[sizeof(int)..]);
        return sizeof(int) + byteCount;
    }

    // The size of the value after it is lower-cased using the invariant culture, which is measured on the lower-cased
    // characters because their UTF-8 length can differ from that of the original characters.
    private static int GetLowerInvariantLengthPrefixedSize(ReadOnlySpan<char> value)
    {
        using var lease = BufferFactory.Rent<char>(
            value.Length,
            isSensitive: false,
            out Span<char> buffer
        );
        var lowered = buffer[..MemoryExtensions.ToLowerInvariant(value, buffer)];
        return GetLengthPrefixedSize(lowered);
    }

    // Lower-cases the value using the invariant culture into a rented buffer and writes it length-prefixed.
    private static int WriteLowerInvariantLengthPrefixed(
        Span<byte> destination,
        ReadOnlySpan<char> value
    )
    {
        using var lease = BufferFactory.Rent<char>(
            value.Length,
            isSensitive: false,
            out Span<char> buffer
        );
        var lowered = buffer[..MemoryExtensions.ToLowerInvariant(value, buffer)];
        return WriteLengthPrefixed(destination, lowered);
    }
}
