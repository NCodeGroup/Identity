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

using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.KeyMaterial;
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
    /// Unwraps the content encryption key (CEK), or substitutes random bytes for it when the encrypted key cannot be
    /// unwrapped to a key of the required length, so the decryption fails at the authentication tag like any tampered
    /// JWE and a recipient never reveals a format, padding or length error of the encrypted key (RFC 7516 §11.5). A key
    /// of the wrong type or size for the algorithm is the caller's configuration error and is still reported.
    /// </summary>
    private static void UnwrapContentKeyOrSubstitute(
        KeyManagementAlgorithm keyManagementAlgorithm,
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedKey,
        Span<byte> contentKey
    )
    {
        ValidateDecryptionKey(keyManagementAlgorithm, secretKey);

        bool unwrapped;
        int bytesWritten;
        try
        {
            unwrapped = keyManagementAlgorithm.TryUnwrapKey(
                secretKey,
                header,
                encryptedKey,
                contentKey,
                out bytesWritten
            );
        }
        catch (Exception exception)
            when (exception is CryptographicException or JoseException or ArgumentException)
        {
            unwrapped = false;
            bytesWritten = 0;
        }

        if (!unwrapped || bytesWritten != contentKey.Length)
        {
            RandomNumberGenerator.Fill(contentKey);
        }
    }

    private static void ValidateDecryptionKey(
        KeyManagementAlgorithm keyManagementAlgorithm,
        SecretKey secretKey
    )
    {
        if (!keyManagementAlgorithm.KeyType.IsInstanceOfType(secretKey))
        {
            throw new ArgumentException(
                $"The secret key was expected to be a type of '{keyManagementAlgorithm.KeyType.FullName}', but '{secretKey.GetType().FullName}' was given instead.",
                nameof(secretKey)
            );
        }

        if (!KeySizesUtility.IsLegalSize(keyManagementAlgorithm.KeyBitSizes, secretKey.KeySizeBits))
        {
            throw new ArgumentException(
                "The secret key does not have a valid size for this cryptographic algorithm.",
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
