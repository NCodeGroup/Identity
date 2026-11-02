// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Provides the ability to validate key sizes using <see cref="KeySizes"/>.
/// </summary>
[PublicAPI]
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

        return size >= legalSize.MinSize
            && size <= legalSize.MaxSize
            && (size - legalSize.MinSize) % legalSize.SkipSize == 0;
    }
}
