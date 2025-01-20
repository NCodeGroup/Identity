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

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides an abstraction for encoding and decoding secrets.
/// </summary>
public interface ISecretEncoding
{
    /// <summary>
    /// Gets the <see cref="string"/> the identifies the encoding type.
    /// </summary>
    string EncodingType { get; }

    /// <summary>
    /// Decodes the <paramref name="encodedValue"/> into a new instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="encodedValue">The encoded value to decode.</param>
    /// <param name="factory">The factory method to create a new instance of <typeparamref name="T"/>.</param>
    /// <typeparam name="T">The type of the instance to create.</typeparam>
    /// <returns>The new instance of <typeparamref name="T"/> created from the <paramref name="encodedValue"/>.</returns>
    T Decode<T>(string encodedValue, Func<Memory<byte>, T> factory);
}
