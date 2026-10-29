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

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Encrypts and decrypts BFF session and login payloads with AES-256-GCM under a key derived from the browser's secret.
/// </summary>
internal static class BffPayloadProtector
{
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    /// <summary>
    /// Encrypts a payload.
    /// </summary>
    /// <param name="secret">The base64url secret that the key is derived from.</param>
    /// <param name="purpose">The HKDF purpose of the key.</param>
    /// <param name="plaintext">The payload to encrypt.</param>
    /// <param name="associatedData">The data that the ciphertext is bound to.</param>
    /// <returns>The nonce, then the ciphertext, then the tag.</returns>
    /// <exception cref="ArgumentException">The secret is not valid.</exception>
    public static byte[] Protect(
        string secret,
        string purpose,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData
    )
    {
        Span<byte> key = stackalloc byte[KeyLength];
        try
        {
            if (!BffKeyDerivation.TryDeriveKey(secret, purpose, key))
                throw new ArgumentException("The secret is not valid.", nameof(secret));

            var output = new byte[NonceLength + plaintext.Length + TagLength];
            var nonce = output.AsSpan(0, NonceLength);
            var ciphertext = output.AsSpan(NonceLength, plaintext.Length);
            var tag = output.AsSpan(NonceLength + plaintext.Length, TagLength);

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Decrypts a payload.
    /// </summary>
    /// <param name="secret">The base64url secret that the key is derived from.</param>
    /// <param name="purpose">The HKDF purpose of the key.</param>
    /// <param name="protectedData">The nonce, the ciphertext and the tag.</param>
    /// <param name="associatedData">The data that the ciphertext is bound to.</param>
    /// <param name="plaintext">Receives the decrypted payload.</param>
    /// <returns><see langword="true"/> when the payload authenticates and decrypts.</returns>
    public static bool TryUnprotect(
        string secret,
        string purpose,
        ReadOnlySpan<byte> protectedData,
        ReadOnlySpan<byte> associatedData,
        IBufferWriter<byte> plaintext
    )
    {
        if (protectedData.Length < NonceLength + TagLength)
            return false;

        Span<byte> key = stackalloc byte[KeyLength];
        try
        {
            if (!BffKeyDerivation.TryDeriveKey(secret, purpose, key))
                return false;

            var length = protectedData.Length - NonceLength - TagLength;
            var nonce = protectedData[..NonceLength];
            var ciphertext = protectedData.Slice(NonceLength, length);
            var tag = protectedData[(NonceLength + length)..];

            using var aes = new AesGcm(key, TagLength);
            var destination = plaintext.GetSpan(length);
            try
            {
                aes.Decrypt(nonce, ciphertext, tag, destination[..length], associatedData);
            }
            catch (CryptographicException)
            {
                return false;
            }

            plaintext.Advance(length);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
