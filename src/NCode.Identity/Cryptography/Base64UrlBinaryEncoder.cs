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

using NCode.Encoders;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as URL-safe Base64 without padding under the <see cref="BinaryEncodings.Base64Url"/> name.
/// </summary>
internal sealed class Base64UrlBinaryEncoder : IBinaryEncoder
{
    /// <inheritdoc />
    public string Name => BinaryEncodings.Base64Url;

    /// <inheritdoc />
    public string Encode(ReadOnlySpan<byte> data) => Base64Url.Encode(data);
}
