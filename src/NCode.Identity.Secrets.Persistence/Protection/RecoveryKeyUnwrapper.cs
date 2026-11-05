// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Unwraps a master key from its recovery copy with the recovery private key, which only the offline rewrap command
/// holds. It is built from a key file that the command reads, is never registered as an <see cref="IKeyWrapper"/>, and
/// so never exists in a serving process.
/// </summary>
/// <remarks>
/// The private key must be an RSA key in the <c>PRIVATE KEY</c> (PKCS #8) PEM format whose modulus is at least 3072
/// bits. A master key is recovered with RSA-OAEP and SHA-256, the padding that <see cref="RecoveryKeyWrapper"/>
/// wraps with.
/// </remarks>
// See ADR-1408.
internal sealed class RecoveryKeyUnwrapper : IDisposable
{
    private const string PrivateKeyLabel = "PRIVATE KEY";

    private const int MinimumModulusBits = 3072;

    private RecoveryKeyUnwrapper(RSA privateKey)
    {
        PrivateKey = privateKey;
        RecoveryKeyId = RecoveryKeyWrapper.ComputeRecoveryKeyId(privateKey);
    }

    private RSA PrivateKey { get; }

    /// <summary>
    /// Gets the identifier of the recovery key, computed from its public half the way
    /// <see cref="RecoveryKeyWrapper.RecoveryKeyId"/> is, so it matches the identifier stored with a recovery copy.
    /// </summary>
    public string RecoveryKeyId { get; }

    /// <summary>
    /// Creates an unwrapper over the RSA private key in PEM text.
    /// </summary>
    /// <param name="pem">The PEM text of an RSA private key with the <c>PRIVATE KEY</c> label.</param>
    /// <returns>The new unwrapper, which the caller disposes.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the text is not an RSA private key of at least 3072 bits. The message never contains key material.
    /// </exception>
    public static RecoveryKeyUnwrapper FromPem(ReadOnlySpan<char> pem)
    {
        if (
            !PemEncoding.TryFind(pem, out var fields)
            || !pem[fields.Label].SequenceEqual(PrivateKeyLabel)
        )
        {
            throw new InvalidOperationException(
                $"The recovery private key must be an RSA private key in PEM format with the '{PrivateKeyLabel}' label."
            );
        }

        var rsa = RSA.Create();
        var succeeded = false;
        try
        {
            try
            {
                // The platform decodes the Base64 data and clears its own buffer.
                rsa.ImportFromPem(pem[fields.Location]);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException(
                    "The recovery private key is not a valid RSA private key.",
                    exception
                );
            }

            if (rsa.KeySize < MinimumModulusBits)
            {
                throw new InvalidOperationException(
                    $"The recovery key must be at least {MinimumModulusBits} bits but it is {rsa.KeySize} bits."
                );
            }

            var unwrapper = new RecoveryKeyUnwrapper(rsa);
            succeeded = true;
            return unwrapper;
        }
        finally
        {
            if (!succeeded)
                rsa.Dispose();
        }
    }

    /// <summary>
    /// Unwraps a recovery copy and writes the master key to a destination.
    /// </summary>
    /// <param name="ciphertext">The base64url text of the recovery copy.</param>
    /// <param name="destination">The writer that receives the master key bytes; the caller makes it sensitive.</param>
    /// <exception cref="CryptographicException">
    /// Thrown when the copy is not valid base64url text or was not wrapped under this recovery key.
    /// </exception>
    public void Unwrap(ReadOnlySpan<char> ciphertext, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var wrappedSize = WrappedKeyCiphertext.GetByteCount(ciphertext);
        using var wrappedLease = BufferFactory.Rent(
            wrappedSize,
            isSensitive: false,
            out Span<byte> wrapped
        );
        WrappedKeyCiphertext.Decode(ciphertext, wrapped);

        using var plaintextLease = BufferFactory.Rent(
            PrivateKey.KeySize / 8,
            isSensitive: true,
            out Span<byte> plaintext
        );
        var bytesWritten = PrivateKey.Decrypt(wrapped, plaintext, RSAEncryptionPadding.OaepSHA256);

        destination.Write(plaintext[..bytesWritten]);
    }

    /// <summary>
    /// Disposes the recovery private key.
    /// </summary>
    public void Dispose()
    {
        PrivateKey.Dispose();
    }
}
