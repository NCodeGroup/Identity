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
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Extensions;

/// <summary>
/// Provides extensions methods for the <see cref="SecretKey"/> abstraction.
/// </summary>
public static class SecretKeyExtensions
{
    /// <summary>
    /// Validates the <paramref name="secretKey"/> is of the expected type and size.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to validate.</param>
    /// <param name="legalKeyBitSizes">Contains a collection of valid key sizes.</param>
    /// <typeparam name="T">The expected type of the secret key.</typeparam>
    /// <returns>The secret key as the expected type.</returns>
    public static T Validate<T>(this SecretKey secretKey, IEnumerable<KeySizes> legalKeyBitSizes)
        where T : SecretKey
    {
        if (secretKey is not T typedSecretKey)
        {
            throw new ArgumentException(
                $"The secret key was expected to be a type of '{typeof(T).FullName}', but '{secretKey.GetType().FullName}' was given instead.",
                nameof(secretKey));
        }

        if (!KeySizesUtility.IsLegalSize(legalKeyBitSizes, secretKey.KeySizeBits))
        {
            throw new ArgumentException(
                "The secret key does not have a valid size for this cryptographic algorithm.",
                nameof(secretKey));
        }

        return typedSecretKey;
    }

    /// <summary>
    /// Factory method to create and initialize an <see cref="AsymmetricAlgorithm"/> instance using the current <c>PKCS#8</c> key material.
    /// </summary>
    /// <param name="secretKey">The <see cref="AsymmetricSecretKey"/> instance.</param>
    /// <param name="factory">The factory method to create an instance of <typeparamref name="T"/>.</param>
    /// <typeparam name="T">The newly initialized instance of <typeparamref name="T"/>.</typeparam>
    /// <returns>The cryptographic algorithm that derives from <see cref="AsymmetricAlgorithm"/>.</returns>
    public static T ExportAlgorithm<T>(this AsymmetricSecretKey secretKey, Func<T> factory)
        where T : AsymmetricAlgorithm
    {
        var algorithm = factory();
        try
        {
            algorithm.ImportPkcs8PrivateKey(secretKey.Pkcs8PrivateKey, out _);
        }
        catch
        {
            algorithm.Dispose();
            throw;
        }

        return algorithm;
    }
}
