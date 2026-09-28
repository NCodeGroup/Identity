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
using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides an abstraction to encode a JOSE token. An instance is created from a
/// <see cref="JoseEncodingOptions"/> (via <see cref="IJoseSerializer.CreateEncoder(JoseSigningOptions)"/> or
/// <see cref="IJoseSerializer.CreateEncoder(JoseEncryptionOptions)"/>) and carries all the credentials and options
/// needed to encode, so the <c>Encode</c> methods only require the payload and an optional destination.
/// </summary>
[PublicAPI]
public abstract class JoseEncoder
{
    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    public abstract void Encode(IBufferWriter<char> tokenWriter, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode<T>(T payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    public abstract void Encode<T>(IBufferWriter<char> tokenWriter, T payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(string payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    public abstract void Encode(IBufferWriter<char> tokenWriter, string payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(ReadOnlySpan<char> payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    public abstract void Encode(IBufferWriter<char> tokenWriter, ReadOnlySpan<char> payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(ReadOnlySpan<byte> payload);
}
