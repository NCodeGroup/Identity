#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Diagnostics;
using JetBrains.Annotations;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.Secrets.Persistence.Encodings;

/// <summary>
/// Provides an implementation for the <see cref="ISecretEncoding"/> abstraction that uses basic encoding.
/// </summary>
[PublicAPI]
public class BasicSecretEncoding : ISecretEncoding
{
    /// <inheritdoc />
    public string EncodingType => SecretEncodingTypes.Basic;

    /// <inheritdoc />
    public T Decode<T>(string encodedValue, Func<Memory<byte>, T> factory)
    {
        var byteCount = Base64Url.GetByteCountForDecode(encodedValue.Length);
        using var _ = CryptoPool.Rent(byteCount, isSensitive: true, out Memory<byte> privateKeyBytes);

        var decodeResult = Base64Url.TryDecode(encodedValue, privateKeyBytes.Span, out var bytesWritten);
        Debug.Assert(decodeResult && bytesWritten == byteCount);

        return factory(privateKeyBytes);
    }
}
