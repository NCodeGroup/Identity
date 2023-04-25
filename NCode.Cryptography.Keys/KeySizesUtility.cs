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

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides the ability to validate key sizes using <see cref="KeySizes"/>.
/// </summary>
public static class KeySizesUtility
{
    /// <summary>
    /// Validates if the specified <paramref name="size"/> is within the range of any of the <paramref name="legalSizes"/>.
    /// </summary>
    /// <param name="legalSizes">Contains a collection of valid key sizes.</param>
    /// <param name="size">Contains the size to validate.</param>
    /// <returns><c>true</c> if the specified <paramref name="size"/> is within the range of any of the <paramref name="legalSizes"/>; otherwise, <c>false</c>.</returns>
    public static bool IsLegalSize(IEnumerable<KeySizes> legalSizes, int size) =>
        legalSizes.Any(legalSize => IsLegalSize(legalSize, size));

    /// <summary>
    /// Validates if the specified <paramref name="size"/> is within the range of <paramref name="legalSize"/>.
    /// </summary>
    /// <param name="legalSize">Contains the range of valid key sizes.</param>
    /// <param name="size">Contains the size to validate.</param>
    /// <returns><c>true</c> if the specified <paramref name="size"/> is within the range of <paramref name="legalSize"/>; otherwise, <c>false</c>.</returns>
    public static bool IsLegalSize(KeySizes legalSize, int size)
    {
        if (legalSize.SkipSize == 0)
        {
            return size == legalSize.MinSize;
        }

        return size >= legalSize.MinSize &&
               size <= legalSize.MaxSize &&
               (size - legalSize.MinSize) % legalSize.SkipSize == 0;
    }
}
