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

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.Extensions;

/// <summary>
/// Provides various extension methods for <see cref="HashAlgorithmName"/>.
/// </summary>
public static class HashAlgorithmNameExtensions
{
    /// <summary>
    /// Gets the hash size, in bits, from the specified <see cref="HashAlgorithmName"/>.
    /// </summary>
    /// <param name="hashAlgorithmName">Contains the <see cref="HashAlgorithmName"/>.</param>
    /// <returns>The hash size, in bits, from the <see cref="HashAlgorithmName"/>.</returns>
    /// <exception cref="ArgumentException">The specified <see cref="HashAlgorithmName"/> is not supported.</exception>
    public static int GetHashSizeBits(this HashAlgorithmName hashAlgorithmName) =>
        hashAlgorithmName.Name switch
        {
            "SHA1" => 160,
            "SHA256" => 256,
            "SHA384" => 384,
            "SHA512" => 512,
            _ => throw new ArgumentException($"The {hashAlgorithmName} hash algorithm is not supported.", nameof(hashAlgorithmName))
        };

    /// <summary>
    /// Gets a <see cref="HashFunctionDelegate"/> that can be used to hash data using the specified <see cref="HashAlgorithmName"/>.
    /// </summary>
    /// <param name="hashAlgorithmName">Contains the <see cref="HashAlgorithmName"/>.</param>
    /// <returns>The <see cref="HashFunctionDelegate"/> that can be used to hash data using the specified <see cref="HashAlgorithmName"/>.</returns>
    /// <exception cref="ArgumentException">The specified <see cref="HashAlgorithmName"/> is not supported.</exception>
    public static HashFunctionDelegate GetHashFunction(this HashAlgorithmName hashAlgorithmName) =>
        hashAlgorithmName.Name switch
        {
            "SHA256" => SHA256.TryHashData,
            "SHA384" => SHA384.TryHashData,
            "SHA512" => SHA512.TryHashData,
            _ => throw new ArgumentException($"The {hashAlgorithmName} hash algorithm is not supported.", nameof(hashAlgorithmName))
        };
}
