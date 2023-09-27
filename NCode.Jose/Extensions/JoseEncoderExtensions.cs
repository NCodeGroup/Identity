#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NCode.CryptoMemory;
using Nerdbank.Streams;

namespace NCode.Jose.Extensions;

/// <summary>
/// Provides extension methods for <see cref="JoseEncoder"/>.
/// </summary>
public static class JoseEncoderExtensions
{
    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JOSE token.</returns>
    public static string Encode<T>(
        this JoseEncoder joseEncoder,
        T payload,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = joseEncoder.JoseSerializer.SerializeJson(
            payload,
            jsonOptions,
            out var payloadBytes);
        joseEncoder.Encode(
            tokenBuffer,
            payloadBytes,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    public static void Encode<T>(
        this JoseEncoder joseEncoder,
        IBufferWriter<char> tokenWriter,
        T payload,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = joseEncoder.JoseSerializer.SerializeJson(
            payload,
            jsonOptions,
            out var bytes);
        joseEncoder.Encode(
            tokenWriter,
            bytes,
            extraHeaders);
    }

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JOSE token.</returns>
    public static string Encode(
        this JoseEncoder joseEncoder,
        string payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        joseEncoder.Encode(
            tokenBuffer,
            payload.AsSpan(),
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public static void Encode(
        this JoseEncoder joseEncoder,
        IBufferWriter<char> tokenWriter,
        string payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        joseEncoder.Encode(
            tokenWriter,
            payload.AsSpan(),
            extraHeaders);
    }

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JOSE token.</returns>
    public static string Encode(
        this JoseEncoder joseEncoder,
        ReadOnlySpan<char> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        joseEncoder.Encode(
            tokenBuffer,
            payload,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public static void Encode(
        this JoseEncoder joseEncoder,
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = Encoding.UTF8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = Encoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        joseEncoder.Encode(
            tokenWriter,
            payloadBytes,
            extraHeaders);
    }

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="joseEncoder">The <see cref="JoseEncoder"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JOSE token.</returns>
    public static string Encode(
        this JoseEncoder joseEncoder,
        ReadOnlySpan<byte> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        joseEncoder.Encode(
            tokenBuffer,
            payload,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }
}
