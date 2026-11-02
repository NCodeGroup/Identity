// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides an implementation of <see cref="IComparer{SecretKey}"/> that compares <see cref="SecretKey"/> instances using
/// the <see cref="KeyMetadata.ExpiresWhen"/> property descending and then the <c>GetHashCode()</c> value
/// ascending. If any compared doesn't have a <see cref="KeyMetadata.ExpiresWhen"/> value, then it is treated as
/// <see cref="DateTimeOffset.MaxValue"/>.
/// </summary>
[PublicAPI]
public class SecretKeyExpiresWhenComparer : IComparer<SecretKey>
{
    /// <summary>
    /// Gets a singleton instance of <see cref="SecretKeyExpiresWhenComparer"/>.
    /// </summary>
    public static SecretKeyExpiresWhenComparer Singleton { get; } = new();

    /// <inheritdoc />
    public int Compare(SecretKey? x, SecretKey? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        var xExpiresWhen = x?.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue;
        var yExpiresWhen = y?.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue;
        var result = yExpiresWhen.CompareTo(xExpiresWhen); // notice that y then x
        if (result != 0)
            return result;

        var xHashCode = x?.GetHashCode() ?? 0;
        var yHashCode = y?.GetHashCode() ?? 0;
        return xHashCode.CompareTo(yHashCode);
    }
}
