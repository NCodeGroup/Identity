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
using NCode.Cryptography.Keys;

namespace NCode.Jose;

/// <summary>
/// Common interface for all <c>JOSE</c> algorithms that require cryptographic key material.
/// </summary>
public interface IKeyedAlgorithm : IAlgorithm
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the key material that is supported by the current cryptographic algorithm.
    /// </summary>
    Type KeyType { get; }

    /// <summary>
    /// Gets the sizes, in bits, of the key material that is supported by the current cryptographic algorithm.
    /// </summary>
    IEnumerable<KeySizes> KeyBitSizes { get; }
}

/// <summary>
/// Base implementation for all <c>JOSE</c> algorithms that require cryptographic key material.
/// </summary>
public abstract class KeyedAlgorithm : Algorithm, IKeyedAlgorithm
{
    /// <inheritdoc />
    public abstract Type KeyType { get; }

    /// <inheritdoc />
    public abstract IEnumerable<KeySizes> KeyBitSizes { get; }

    /// <summary>
    /// Gets the hash size, in bits, from the specified <see cref="HashAlgorithmName"/>.
    /// </summary>
    /// <param name="hashAlgorithmName">Contains the <see cref="HashAlgorithmName"/>.</param>
    /// <returns>The hash size, in bits, from the <see cref="HashAlgorithmName"/>.</returns>
    /// <exception cref="ArgumentException">The specified <see cref="HashAlgorithmName"/> is not supported.</exception>
    protected static int HashSizeBitsFromAlgorithmName(HashAlgorithmName hashAlgorithmName)
    {
        if (hashAlgorithmName == HashAlgorithmName.SHA256)
            return 256;
        if (hashAlgorithmName == HashAlgorithmName.SHA384)
            return 384;
        if (hashAlgorithmName == HashAlgorithmName.SHA512)
            return 512;
        throw new ArgumentException("Unsupported hash algorithm.", nameof(hashAlgorithmName));
    }

    /// <summary>
    /// Validates that the specified <paramref name="secretKey"/> is an instance of <typeparamref name="T"/>
    /// and verifies the key size.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to validate.</param>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns><paramref name="secretKey"/> casted to <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is not a type of <typeparamref name="T"/>
    /// or when the key size was invalid.</exception>
    protected T ValidateSecretKey<T>(SecretKey secretKey)
        where T : SecretKey
    {
        if (secretKey is not T typedSecretKey)
        {
            throw new ArgumentException(
                $"The secret key was expected to be a type of {typeof(T).FullName}, but {secretKey.GetType().FullName} was given instead.",
                nameof(secretKey));
        }

        if (!KeySizesUtility.IsLegalSize(KeyBitSizes, secretKey.KeySizeBits))
        {
            throw new ArgumentException(
                "The secret key does not have a valid size for this cryptographic algorithm.",
                nameof(secretKey));
        }

        return typedSecretKey;
    }
}
