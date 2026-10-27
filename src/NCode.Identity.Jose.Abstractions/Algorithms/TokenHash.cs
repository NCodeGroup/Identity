#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using NCode.Buffers;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Computes and verifies the token hash claims of an ID token: <c>at_hash</c>, <c>c_hash</c> and <c>s_hash</c>.
/// </summary>
/// <remarks>
/// The value of a token hash claim is the base64url encoding of the left-most half of the hash of the octets of the
/// ASCII representation of the value, where the hash function is the one that the <c>alg</c> header parameter of the
/// ID token defines (OpenID Connect Core 1.0 section 3.1.3.6). Obtain the hash function and its output size from
/// <see cref="SignatureAlgorithm.GetTokenHashFunction"/>.
/// </remarks>
[PublicAPI]
public static class TokenHash
{
    /// <summary>
    /// Computes a token hash claim value: the base64url encoding of the left-most half of the hash of the ASCII
    /// octets of <paramref name="value"/> (OpenID Connect Core 1.0 section 3.1.3.6).
    /// </summary>
    /// <param name="hashFunction">The hash function to apply to the value.</param>
    /// <param name="hashSizeBytes">The number of bytes that <paramref name="hashFunction"/> produces.</param>
    /// <param name="value">The access token, authorization code or state to hash. It must be ASCII.</param>
    /// <returns>The value of the token hash claim.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> contains a character that is not ASCII.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="hashFunction"/> failed or did not produce <paramref name="hashSizeBytes"/> bytes.
    /// </exception>
    public static string Compute(HashFunctionDelegate hashFunction, int hashSizeBytes, string value)
    {
        ArgumentNullException.ThrowIfNull(hashFunction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hashSizeBytes);
        ArgumentNullException.ThrowIfNull(value);

        if (!Ascii.IsValid(value))
            throw new ArgumentException(
                "The value must contain only ASCII characters.",
                nameof(value)
            );

        var source = SecureEncoding.ASCII.GetBytes(value);

        Span<byte> hash = stackalloc byte[hashSizeBytes];
        if (!hashFunction(source, hash, out var bytesWritten) || bytesWritten != hashSizeBytes)
            throw new InvalidOperationException(
                "The hash function did not produce the expected number of bytes."
            );

        return Base64Url.EncodeToString(hash[..(hashSizeBytes / 2)]);
    }

    /// <summary>
    /// Verifies a token hash claim value against the value that it covers. The comparison runs in constant time.
    /// </summary>
    /// <param name="hashFunction">The hash function to apply to the value.</param>
    /// <param name="hashSizeBytes">The number of bytes that <paramref name="hashFunction"/> produces.</param>
    /// <param name="value">The access token, authorization code or state that the claim covers.</param>
    /// <param name="tokenHash">The value of the token hash claim.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="tokenHash"/> is the token hash of <paramref name="value"/>; otherwise,
    /// <c>false</c>, including when either argument is not ASCII.
    /// </returns>
    public static bool Verify(
        HashFunctionDelegate hashFunction,
        int hashSizeBytes,
        string value,
        string tokenHash
    )
    {
        ArgumentNullException.ThrowIfNull(hashFunction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hashSizeBytes);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tokenHash);

        if (!Ascii.IsValid(value) || !Ascii.IsValid(tokenHash))
            return false;

        var expected = Compute(hashFunction, hashSizeBytes, value);

        return CryptographicOperations.FixedTimeEquals(
            SecureEncoding.ASCII.GetBytes(expected),
            SecureEncoding.ASCII.GetBytes(tokenHash)
        );
    }
}
