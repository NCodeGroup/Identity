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

using System.Buffers;
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
/// <see cref="SignatureAlgorithm.GetTokenHashFunction"/>, and allocate a buffer of that size once: the overloads
/// take it as <c>hashBuffer</c>, so a caller can reuse it for the several values of one ID token.
/// </remarks>
[PublicAPI]
public static class TokenHash
{
    private const int StackAllocLimit = 256;

    /// <summary>
    /// Computes a token hash claim value into a caller-supplied buffer: the base64url encoding of the left-most half
    /// of the hash of the ASCII octets of <paramref name="value"/> (OpenID Connect Core 1.0 section 3.1.3.6).
    /// </summary>
    /// <param name="hashFunction">The hash function to apply to the value.</param>
    /// <param name="value">The access token, authorization code or state to hash. It must be ASCII.</param>
    /// <param name="hashBuffer">
    /// The buffer that receives the full hash. Its length is the output size of <paramref name="hashFunction"/>, as
    /// <see cref="SignatureAlgorithm.GetTokenHashFunction"/> reports it. The buffer is overwritten, so a caller can
    /// reuse one buffer for several values.
    /// </param>
    /// <returns>The value of the token hash claim.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="hashBuffer"/> is empty, or <paramref name="value"/> contains a character that is not ASCII.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="hashFunction"/> failed or did not produce as many bytes as the length of
    /// <paramref name="hashBuffer"/>.
    /// </exception>
    public static string Compute(
        HashFunctionDelegate hashFunction,
        ReadOnlySpan<char> value,
        Span<byte> hashBuffer
    )
    {
        ArgumentNullException.ThrowIfNull(hashFunction);

        HashAscii(hashFunction, value, hashBuffer);

        return Base64Url.EncodeToString(hashBuffer[..(hashBuffer.Length / 2)]);
    }

    /// <summary>
    /// Verifies a token hash claim value against the value that it covers, using a caller-supplied buffer. The
    /// comparison runs in constant time.
    /// </summary>
    /// <param name="hashFunction">The hash function to apply to the value.</param>
    /// <param name="value">The access token, authorization code or state that the claim covers.</param>
    /// <param name="tokenHash">The value of the token hash claim.</param>
    /// <param name="hashBuffer">
    /// The buffer that receives the full hash. Its length is the output size of <paramref name="hashFunction"/>, as
    /// <see cref="SignatureAlgorithm.GetTokenHashFunction"/> reports it. The buffer is overwritten, so a caller can
    /// reuse one buffer for several values.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="tokenHash"/> is the token hash of <paramref name="value"/>; otherwise,
    /// <c>false</c>, including when <paramref name="hashBuffer"/> is empty or either argument is not ASCII.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="hashFunction"/> failed or did not produce as many bytes as the length of
    /// <paramref name="hashBuffer"/>.
    /// </exception>
    public static bool Verify(
        HashFunctionDelegate hashFunction,
        ReadOnlySpan<char> value,
        ReadOnlySpan<char> tokenHash,
        Span<byte> hashBuffer
    )
    {
        ArgumentNullException.ThrowIfNull(hashFunction);

        if (hashBuffer.IsEmpty || !Ascii.IsValid(value) || !Ascii.IsValid(tokenHash))
            return false;

        // The length of the claim is not secret, so a difference in length returns early.
        var halfLength = hashBuffer.Length / 2;
        var encodedLength = Base64Url.GetEncodedLength(halfLength);
        if (tokenHash.Length != encodedLength)
            return false;

        HashAscii(hashFunction, value, hashBuffer);

        Span<byte> expected =
            encodedLength <= StackAllocLimit
                ? stackalloc byte[encodedLength]
                : new byte[encodedLength];
        Base64Url.EncodeToUtf8(hashBuffer[..halfLength], expected, out _, out _);

        Span<byte> claim =
            encodedLength <= StackAllocLimit
                ? stackalloc byte[encodedLength]
                : new byte[encodedLength];
        Ascii.FromUtf16(tokenHash, claim, out _);

        return CryptographicOperations.FixedTimeEquals(expected, claim);
    }

    private static void HashAscii(
        HashFunctionDelegate hashFunction,
        ReadOnlySpan<char> value,
        Span<byte> hashBuffer
    )
    {
        if (hashBuffer.IsEmpty)
            throw new ArgumentException("The hash buffer must not be empty.", nameof(hashBuffer));

        IDisposable? lease = null;
        try
        {
            // ReSharper disable once UseCollectionExpression
            Span<byte> source = stackalloc byte[0];

            if (value.Length <= StackAllocLimit)
            {
                source = stackalloc byte[StackAllocLimit];
            }
            else
            {
                lease = BufferFactory.Rent(value.Length, isSensitive: false, out source);
            }

            var status = Ascii.FromUtf16(value, source, out var written);
            if (status != OperationStatus.Done)
                throw new ArgumentException(
                    "The value must contain only ASCII characters.",
                    nameof(value)
                );

            if (
                !hashFunction(source[..written], hashBuffer, out var bytesWritten)
                || bytesWritten != hashBuffer.Length
            )
                throw new InvalidOperationException(
                    "The hash function did not produce the expected number of bytes."
                );
        }
        finally
        {
            lease?.Dispose();
        }
    }
}
