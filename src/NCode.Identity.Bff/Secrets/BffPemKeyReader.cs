// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Bff.Secrets;

/// <summary>
/// Reads one PEM-encoded EC or RSA private key of a <c>Bff:KeyRings:{name}</c> entry; its id is the file name without
/// the extension.
/// </summary>
// See ADR-1439.
internal static class BffPemKeyReader
{
    /// <summary>
    /// Reads the PEM file of one key of a configured key ring.
    /// </summary>
    /// <param name="keyFactory">The <see cref="ISecretKeyFactory"/> that creates the key.</param>
    /// <param name="keyRing">The name of the key ring within <c>Bff:KeyRings</c>.</param>
    /// <param name="path">The full path of the PEM file.</param>
    /// <returns>The <see cref="SecretKey"/> that holds the private key.</returns>
    /// <exception cref="InvalidOperationException">The file holds no PEM-encoded EC or RSA private key.</exception>
    public static SecretKey Read(ISecretKeyFactory keyFactory, string keyRing, string path)
    {
        ArgumentNullException.ThrowIfNull(keyFactory);
        ArgumentException.ThrowIfNullOrEmpty(keyRing);
        ArgumentException.ThrowIfNullOrEmpty(path);

        return ReadKey(keyFactory, keyRing, path);
    }

    private static SecretKey ReadKey(ISecretKeyFactory keyFactory, string name, string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);

        var length = checked((int)RandomAccess.GetLength(handle));
        using var bytesLease = BufferFactory.Rent(length, isSensitive: true, out Span<byte> buffer);
        var bytesRead = RandomAccess.Read(handle, buffer, 0);
        var bytes = buffer[..bytesRead];

        var charCount = SecureEncoding.UTF8.GetCharCount(bytes);
        using var charsLease = BufferFactory.Rent(
            charCount,
            isSensitive: true,
            out Span<char> chars
        );
        var charsWritten = SecureEncoding.UTF8.GetChars(bytes, chars);
        var pem = chars[..charsWritten];

        var metadata = new KeyMetadata { KeyId = Path.GetFileNameWithoutExtension(path) };

        try
        {
            return keyFactory.CreateEccPem(metadata, pem);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // The file holds no EC private key; it may hold an RSA one.
        }

        try
        {
            return keyFactory.CreateRsaPem(metadata, pem);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The key ring '{name}' cannot read '{path}': it holds no PEM-encoded EC or RSA private key.",
                ex
            );
        }
    }
}
