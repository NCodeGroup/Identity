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

namespace NCode.Identity.Jose.Encoders;

/// <summary>
/// Provides an implementation of <see cref="JoseEncoder"/> that can be used to encrypt JWE tokens.
/// </summary>
public class JoseEncryptionEncoder : CommonJoseEncoder
{
    private JoseEncryptionOptions EncryptionOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseEncryptionEncoder"/> class with the specified encryption credentials and options.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="JoseSerializer"/> instance.</param>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    public JoseEncryptionEncoder(
        JoseSerializer joseSerializer,
        JoseEncryptionOptions encryptionOptions)
        : base(joseSerializer)
    {
        EncryptionOptions = encryptionOptions;
    }

    /// <inheritdoc />
    public override void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null) =>
        JoseSerializer.Encode(
            tokenWriter,
            payload,
            EncryptionOptions,
            extraHeaders);
}
