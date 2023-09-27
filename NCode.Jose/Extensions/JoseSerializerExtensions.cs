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
/// Provides various extension methods for the <see cref="IJoseSerializer"/> interface.
/// </summary>
public static class JoseSerializerExtensions
{
    #region Sign JWS

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JWS token.</returns>
    public static string Encode<T>(
        this IJoseSerializer joseSerializer,
        T payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = joseSerializer.SerializeJson(
            payload,
            jsonOptions,
            out var payloadBytes);
        joseSerializer.Encode(
            tokenBuffer,
            payloadBytes,
            signingCredentials,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    public static void Encode<T>(
        this IJoseSerializer joseSerializer,
        IBufferWriter<char> tokenWriter,
        T payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = joseSerializer.SerializeJson(
            payload,
            jsonOptions,
            out var bytes);
        joseSerializer.Encode(
            tokenWriter,
            bytes,
            signingCredentials,
            signingOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JWS token.</returns>
    public static string Encode(
        this IJoseSerializer joseSerializer,
        string payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        joseSerializer.Encode(
            tokenBuffer,
            payload.AsSpan(),
            signingCredentials,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public static void Encode(
        this IJoseSerializer joseSerializer,
        IBufferWriter<char> tokenWriter,
        string payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        joseSerializer.Encode(
            tokenWriter,
            payload.AsSpan(),
            signingCredentials,
            signingOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JWS token.</returns>
    public static string Encode(
        this IJoseSerializer joseSerializer,
        ReadOnlySpan<char> payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        joseSerializer.Encode(
            tokenBuffer,
            payload,
            signingCredentials,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public static void Encode(
        this IJoseSerializer joseSerializer,
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = Encoding.UTF8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = Encoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        joseSerializer.Encode(
            tokenWriter,
            payloadBytes,
            signingCredentials,
            signingOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <returns>The encoded JWS token.</returns>
    public static string Encode(
        this IJoseSerializer joseSerializer,
        ReadOnlySpan<byte> payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        joseSerializer.Encode(
            tokenBuffer,
            payload,
            signingCredentials,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    #endregion

    #region Encrypt JWE

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encrypted JWE token.</returns>
    public static string Encode<T>(
        this IJoseSerializer joseSerializer,
        T payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = joseSerializer.SerializeJson(
            payload,
            jsonOptions,
            out var payloadBytes);
        joseSerializer.Encode(
            tokenBuffer,
            payloadBytes,
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    public static void Encode<T>(
        this IJoseSerializer joseSerializer,
        IBufferWriter<char> tokenWriter,
        T payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = joseSerializer.SerializeJson(
            payload,
            jsonOptions,
            out var payloadBytes);
        joseSerializer.Encode(
            tokenWriter,
            payloadBytes,
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public static void Encode(
        this IJoseSerializer joseSerializer,
        IBufferWriter<char> tokenWriter,
        string payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        joseSerializer.Encode(
            tokenWriter,
            payload.AsSpan(),
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    public static string Encode(
        this IJoseSerializer joseSerializer,
        string payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        return joseSerializer.Encode(
            payload.AsSpan(),
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public static void Encode(
        this IJoseSerializer joseSerializer,
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = Encoding.UTF8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = Encoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        joseSerializer.Encode(
            tokenWriter,
            payloadBytes,
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
    }

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    public static string Encode(
        this IJoseSerializer joseSerializer,
        ReadOnlySpan<char> payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        joseSerializer.Encode(
            tokenBuffer,
            payload,
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionCredentials">The JOSE encryption credentials.</param>
    /// <param name="encryptionOptions">The JOSE encryption options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    public static string Encode(
        this IJoseSerializer joseSerializer,
        ReadOnlySpan<byte> payload,
        JoseEncryptionCredentials encryptionCredentials,
        JoseEncryptionOptions? encryptionOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        joseSerializer.Encode(
            tokenBuffer,
            payload,
            encryptionCredentials,
            encryptionOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    #endregion
}
