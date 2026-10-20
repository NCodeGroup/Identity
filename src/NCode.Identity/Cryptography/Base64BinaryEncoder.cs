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

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as standard Base64 with padding under the <see cref="BinaryEncodings.Base64"/> name.
/// </summary>
internal sealed class Base64BinaryEncoder : IBinaryEncoder
{
    /// <inheritdoc />
    public string Name => BinaryEncodings.Base64;

    /// <inheritdoc />
    public string Encode(ReadOnlySpan<byte> data) => Convert.ToBase64String(data);
}
