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
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides an abstraction to encode a JOSE token.
/// </summary>
public abstract class JoseEncoder
{
    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public abstract void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode<T>(
        T payload,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    public abstract void Encode<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(
        string payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public abstract void Encode(
        IBufferWriter<char> tokenWriter,
        string payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(
        ReadOnlySpan<char> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    public abstract void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(
        ReadOnlySpan<byte> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);
}
