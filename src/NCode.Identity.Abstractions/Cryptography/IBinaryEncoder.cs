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

using JetBrains.Annotations;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as text under one registered encoding name.
/// </summary>
/// <remarks>
/// Register an implementation in the <see cref="IBinaryEncoder"/> strategy collection to add an encoding;
/// <see cref="ICryptoService"/> resolves encoders by <see cref="Name"/> and rejects a duplicate or unknown name.
/// </remarks>
[PublicAPI]
public interface IBinaryEncoder
{
    /// <summary>
    /// Gets the name of the encoding, such as a <see cref="BinaryEncodings"/> name. Names compare ordinally.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Encodes binary data as text.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    /// <returns>The binary data encoded as text.</returns>
    string Encode(ReadOnlySpan<byte> data);
}
