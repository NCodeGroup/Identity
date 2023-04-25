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
/// Common interface for all <c>JOSE</c> cryptographic algorithms.
/// </summary>
public interface IAlgorithm
{
    /// <summary>
    /// Gets an <see cref="AlgorithmType"/> value that describes the type of the current cryptographic algorithm.
    /// </summary>
    AlgorithmType Type { get; }

    /// <summary>
    /// Gets a <see cref="string"/> value that uniquely identifies the current cryptographic algorithm.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Gets the <see cref="Type"/> of the <see cref="SecretKey"/> supported by the current cryptographic algorithm.
    /// </summary>
    Type SecretKeyType { get; }

    /// <summary>
    /// Gets the legal key sizes, in bits, that are supported by the current cryptographic algorithm.
    /// </summary>
    IEnumerable<KeySizes> KekBitSizes { get; }
}

/// <summary>
/// Base implementation for all <c>JOSE</c> cryptographic algorithms.
/// </summary>
public abstract class Algorithm
{
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
    /// Validates that the specified <paramref name="secretKey"/> is an instance of <typeparamref name="T"/> and optionally
    /// verifies the key size.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to validate.</param>
    /// <param name="legalBitSizes">An optional argument to validate the size of the <see cref="SecretKey"/>.</param>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns><paramref name="secretKey"/> casted to <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is not a type of <typeparamref name="T"/>
    /// or when the key size was invalid.</exception>
    protected static T ValidateSecretKey<T>(SecretKey secretKey, IEnumerable<KeySizes>? legalBitSizes = null)
        where T : SecretKey
    {
        if (secretKey is not T typedSecretKey)
        {
            throw new ArgumentException(
                $"The security key was expected to be a type of {typeof(T).FullName}, but {secretKey.GetType().FullName} was given instead.",
                nameof(secretKey));
        }

        var isLegalSize = legalBitSizes == null || KeySizesUtility.IsLegalSize(legalBitSizes, secretKey.KeySizeBits);
        if (!isLegalSize)
        {
            throw new ArgumentException(
                "The specified secret key does not have a valid size for this cryptographic algorithm.",
                nameof(secretKey));
        }

        return typedSecretKey;
    }
}
