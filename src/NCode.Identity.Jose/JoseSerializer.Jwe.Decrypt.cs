// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    /// <summary>
    /// The message of every failure to decrypt a JWE that depends on its encrypted key, ciphertext or tag, so a failed
    /// key unwrap reads exactly like a failed authentication tag (RFC 7516 §11.5).
    /// </summary>
    internal const string JweDecryptionFailedMessage = "Failed to decrypt the JWE.";

    /// <summary>
    /// Unwraps the content encryption key (CEK) asynchronously, so a key that a key management service holds can
    /// unwrap it, and substitutes random bytes for it when the encrypted key cannot be unwrapped to a key of the
    /// required length, so the decryption fails at the authentication tag like any tampered JWE and a recipient never
    /// reveals a format, padding or length error of the encrypted key (RFC 7516 §11.5). A key of the wrong type, size
    /// or curve for the algorithm is the caller's configuration error and is still reported. Any other exception, such
    /// as a transport failure of a key management service or cancellation, propagates.
    /// </summary>
    private static async ValueTask UnwrapContentKeyOrSubstituteAsync(
        KeyManagementAlgorithm keyManagementAlgorithm,
        SecretKey secretKey,
        JsonElement header,
        ReadOnlyMemory<byte> encryptedKey,
        Memory<byte> contentKey,
        CancellationToken cancellationToken
    )
    {
        ValidateDecryptionKey(keyManagementAlgorithm, secretKey);

        int bytesWritten;
        try
        {
            bytesWritten = await keyManagementAlgorithm.UnwrapKeyAsync(
                secretKey,
                header,
                encryptedKey,
                contentKey,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception is CryptographicException or JoseException or ArgumentException)
        {
            bytesWritten = -1;
        }

        if (bytesWritten != contentKey.Length)
        {
            RandomNumberGenerator.Fill(contentKey.Span);
        }
    }

    private static void ValidateDecryptionKey(
        KeyManagementAlgorithm keyManagementAlgorithm,
        SecretKey secretKey
    )
    {
        // See ADR-1389: an algorithm may accept several key types, each with its own sizes.
        if (!keyManagementAlgorithm.TryGetKeyType(secretKey.GetType(), out var algorithmKeyType))
        {
            var expected = string.Join(
                "', '",
                keyManagementAlgorithm.KeyTypes.Select(keyType => keyType.KeyClrType.FullName)
            );
            throw new ArgumentException(
                $"The secret key was expected to be a type of '{expected}', but '{secretKey.GetType().FullName}' was given instead.",
                nameof(secretKey)
            );
        }

        if (!algorithmKeyType.IsLegalSize(secretKey.KeySizeBits))
        {
            throw new ArgumentException(
                "The secret key does not have a valid size for this cryptographic algorithm.",
                nameof(secretKey)
            );
        }

        // Two curves can share a type and a size, such as Ed25519 and X25519, so the algorithm also judges the key. The
        // check reads only the configured key, never the token, so it reveals nothing about a ciphertext
        // (RFC 7516 §11.5).
        if (!keyManagementAlgorithm.IsKeyCompatible(secretKey))
        {
            throw new ArgumentException(
                "The secret key's curve or parameters are not valid for this cryptographic algorithm.",
                nameof(secretKey)
            );
        }
    }

    /// <summary>
    /// Decrypts the JWE ciphertext and verifies its authentication tag, and reports every failure with one message and
    /// no inner exception, so the cause of a failure is never observable (RFC 7516 §11.5).
    /// </summary>
    /// <returns>The number of plaintext bytes written.</returns>
    private static int DecryptContentOrThrow(
        AuthenticatedEncryptionAlgorithm authenticatedEncryptionAlgorithm,
        ReadOnlySpan<byte> contentKey,
        ReadOnlySpan<byte> initializationVector,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText
    )
    {
        bool decrypted;
        int bytesWritten;
        try
        {
            decrypted = authenticatedEncryptionAlgorithm.TryDecrypt(
                contentKey,
                initializationVector,
                cipherText,
                associatedData,
                authenticationTag,
                plainText,
                out bytesWritten
            );
        }
        catch (Exception exception)
            when (exception is CryptographicException or JoseException or ArgumentException)
        {
            decrypted = false;
            bytesWritten = 0;
        }

        if (!decrypted || bytesWritten == 0)
            throw new JoseEncryptionException(JweDecryptionFailedMessage);

        return bytesWritten;
    }
}
