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

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extensions methods for the <see cref="SecretKey"/> abstraction.
/// </summary>
[PublicAPI]
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
}
