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
using JetBrains.Annotations;
using NCode.Encoders;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Decodes the ciphertext of a wrapped key that a key wrapper stores as base64url text (RFC 4648 §5, no padding).
/// </summary>
// See ADR-0279.
[PublicAPI]
public static class WrappedKeyCiphertext
{
    private const string NotBase64UrlMessage = "The wrapped key is not valid base64url text.";

    /// <summary>
    /// Gets the number of bytes that the base64url text of a wrapped key decodes to.
    /// </summary>
    /// <param name="ciphertext">The base64url text of the wrapped key.</param>
    /// <returns>The number of bytes the text decodes to.</returns>
    /// <exception cref="CryptographicException">The text is empty or is not valid base64url text.</exception>
    public static int GetByteCount(ReadOnlySpan<char> ciphertext)
    {
        if (ciphertext.IsEmpty)
            throw new CryptographicException("The wrapped key is empty.");

        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (ciphertext.Length % 4 == 1)
            throw new CryptographicException(NotBase64UrlMessage);

        return Base64Url.GetByteCountForDecode(ciphertext.Length);
    }

    /// <summary>
    /// Decodes the base64url text of a wrapped key into a destination buffer.
    /// </summary>
    /// <param name="ciphertext">The base64url text of the wrapped key.</param>
    /// <param name="destination">The buffer that receives the decoded bytes.</param>
    /// <returns>The number of bytes written, which equals <see cref="GetByteCount"/>.</returns>
    /// <exception cref="CryptographicException">The text is empty or is not valid base64url text.</exception>
    /// <exception cref="ArgumentException">The destination is shorter than <see cref="GetByteCount"/>.</exception>
    public static int Decode(ReadOnlySpan<char> ciphertext, Span<byte> destination)
    {
        _ = GetByteCount(ciphertext);

        bool decoded;
        int bytesWritten;
        try
        {
            decoded = Base64Url.TryDecode(ciphertext, destination, out bytesWritten);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(NotBase64UrlMessage, exception);
        }

        if (!decoded)
            throw new ArgumentException(
                "The destination is too small to hold the decoded wrapped key.",
                nameof(destination)
            );

        return bytesWritten;
    }

    /// <summary>
    /// Decodes the base64url text of a wrapped key into a buffer writer.
    /// </summary>
    /// <typeparam name="TWriter">The type of the buffer writer.</typeparam>
    /// <param name="ciphertext">The base64url text of the wrapped key.</param>
    /// <param name="destination">The writer that receives the decoded bytes; it advances by <see cref="GetByteCount"/> bytes.</param>
    /// <exception cref="CryptographicException">The text is empty or is not valid base64url text; the writer does not advance.</exception>
    public static void Decode<TWriter>(ReadOnlySpan<char> ciphertext, ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var byteCount = GetByteCount(ciphertext);
        var span = destination.GetSpan(byteCount);
        var bytesWritten = Decode(ciphertext, span);
        destination.Advance(bytesWritten);
    }
}
