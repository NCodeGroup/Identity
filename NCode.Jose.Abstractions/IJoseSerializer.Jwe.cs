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

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified encrypting credentials and options.
    /// </summary>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(
        JoseEncryptionOptions encryptionOptions);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encrypted JWE token.</returns>
    string Encode<T>(
        T payload,
        JoseEncryptionOptions encryptionOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="jsonOptions">The options to control JSON serialization behavior.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    void Encode<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        JoseEncryptionOptions encryptionOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        string payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    string Encode(
        string payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    string Encode(
        ReadOnlySpan<char> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    string Encode(
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);
}
