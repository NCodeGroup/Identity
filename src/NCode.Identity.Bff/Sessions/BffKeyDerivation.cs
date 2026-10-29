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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Derives the opaque identifiers and encryption keys of BFF sessions and login transactions from the high-entropy
/// secrets that the browser carries, so the server never stores the secret itself.
/// </summary>
internal static class BffKeyDerivation
{
    /// <summary>
    /// The HKDF purpose that derives a session's store identifier.
    /// </summary>
    public const string SessionIdPurpose = "ncode.bff.session.id";

    /// <summary>
    /// The HKDF purpose that derives a session's payload encryption key.
    /// </summary>
    public const string SessionPayloadPurpose = "ncode.bff.session.payload";

    /// <summary>
    /// The HKDF purpose that derives a login transaction's store identifier.
    /// </summary>
    public const string LoginIdPurpose = "ncode.bff.login.id";

    /// <summary>
    /// The HKDF purpose that derives a login transaction's payload encryption key.
    /// </summary>
    public const string LoginPayloadPurpose = "ncode.bff.login.payload";

    /// <summary>
    /// The length of a base64url-encoded 32-byte secret.
    /// </summary>
    public const int SecretLength = 43;

    private const int SecretByteCount = 32;

    /// <summary>
    /// Creates a new random 256-bit secret as a base64url string.
    /// </summary>
    public static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[SecretByteCount];
        RandomNumberGenerator.Fill(bytes);
        try
        {
            return Base64Url.Encode(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>
    /// Decodes a secret into 32 bytes.
    /// </summary>
    /// <param name="secret">The base64url secret.</param>
    /// <param name="destination32">The 32-byte destination.</param>
    /// <returns><see langword="true"/> when the secret is exactly 43 valid characters.</returns>
    public static bool TryDecodeSecret(string? secret, Span<byte> destination32)
    {
        if (
            secret is null
            || secret.Length != SecretLength
            || destination32.Length != SecretByteCount
        )
            return false;

        Span<byte> buffer = stackalloc byte[SecretByteCount + 2];
        try
        {
            int written;
            try
            {
                if (!Base64Url.TryDecode(secret, buffer, out written))
                    return false;
            }
            catch (FormatException)
            {
                return false;
            }

            if (written != SecretByteCount)
                return false;

            buffer[..SecretByteCount].CopyTo(destination32);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    /// <summary>
    /// Derives a 32-byte key from a secret for the given purpose.
    /// </summary>
    /// <param name="secret">The base64url secret.</param>
    /// <param name="purpose">The HKDF purpose.</param>
    /// <param name="destination32">The 32-byte destination.</param>
    /// <returns><see langword="true"/> when the secret is valid.</returns>
    public static bool TryDeriveKey(string? secret, string purpose, Span<byte> destination32)
    {
        Span<byte> ikm = stackalloc byte[SecretByteCount];
        try
        {
            if (!TryDecodeSecret(secret, ikm))
                return false;

            var info = SecureEncoding.UTF8.GetBytes(purpose);
            HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, destination32, salt: [], info: info);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ikm);
        }
    }

    /// <summary>
    /// Derives an opaque identifier from a secret for the given purpose.
    /// </summary>
    /// <param name="secret">The base64url secret.</param>
    /// <param name="purpose">The HKDF purpose.</param>
    /// <param name="identifier">The base64url identifier.</param>
    /// <returns><see langword="true"/> when the secret is valid.</returns>
    public static bool TryDeriveIdentifier(
        string? secret,
        string purpose,
        [NotNullWhen(true)] out string? identifier
    )
    {
        Span<byte> key = stackalloc byte[SecretByteCount];
        try
        {
            if (!TryDeriveKey(secret, purpose, key))
            {
                identifier = null;
                return false;
            }

            identifier = Base64Url.Encode(key);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Computes the base64url SHA-256 fingerprint of a value.
    /// </summary>
    /// <param name="value">The value to fingerprint.</param>
    public static string Fingerprint(string value)
    {
        var bytes = SecureEncoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Base64Url.Encode(hash);
    }
}
