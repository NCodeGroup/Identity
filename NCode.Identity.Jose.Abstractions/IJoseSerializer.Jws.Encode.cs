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
using System.Text.Json;

namespace NCode.Identity.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified signing credentials and options.
    /// </summary>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(
        JoseSigningOptions signingOptions);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JWS token.</returns>
    string Encode<T>(
        T payload,
        JoseSigningOptions signingOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    void Encode<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        JoseSigningOptions signingOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JWS token.</returns>
    string Encode(
        string payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        string payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JWS token.</returns>
    string Encode(
        ReadOnlySpan<char> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <returns>The encoded JWS token.</returns>
    string Encode(
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);
}
