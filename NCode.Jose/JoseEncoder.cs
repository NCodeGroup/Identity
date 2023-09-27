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

namespace NCode.Jose;

/// <summary>
/// Provides an abstraction to encode a JOSE token.
/// </summary>
public abstract class JoseEncoder
{
    /// <summary>
    /// Gets the <see cref="IJoseSerializer"/> instance.
    /// </summary>
    public IJoseSerializer JoseSerializer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseEncoder"/> class.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="IJoseSerializer"/> instance.</param>
    protected JoseEncoder(IJoseSerializer joseSerializer)
    {
        JoseSerializer = joseSerializer;
    }

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
}
