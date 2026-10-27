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
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Common abstractions for all <c>JOSE</c> algorithms that require cryptographic key material.
/// </summary>
[PublicAPI]
public abstract class KeyedAlgorithm : Algorithm
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the key material that is supported by the current cryptographic algorithm.
    /// </summary>
    public abstract Type KeyType { get; }

    /// <summary>
    /// Gets the sizes, in bits, of the key material that is supported by the current cryptographic algorithm.
    /// </summary>
    public abstract IEnumerable<KeySizes> KeyBitSizes { get; }

    /// <summary>
    /// Determines whether a secret key can be used with this algorithm. The default checks the key's type
    /// against <see cref="KeyType"/> and its size against <see cref="KeyBitSizes"/>; an algorithm that is bound
    /// to a specific curve overrides it to check the curve too.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to check.</param>
    /// <returns><c>true</c> if the key can be used with this algorithm; otherwise, <c>false</c>.</returns>
    public virtual bool IsKeyCompatible(SecretKey secretKey) =>
        KeyType.IsInstanceOfType(secretKey)
        && KeySizesUtility.IsLegalSize(KeyBitSizes, secretKey.KeySizeBits);
}
