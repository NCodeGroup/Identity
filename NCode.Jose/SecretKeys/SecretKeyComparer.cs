﻿#region Copyright Preamble

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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides an implementation of <see cref="IComparer{SecretKey}"/> that compares <see cref="SecretKey"/> instances using the <see cref="KeyMetadata.ExpiresWhen"/> property.
/// </summary>
public class SecretKeyExpiresWhenComparer : IComparer<SecretKey>
{
    /// <summary>
    /// Gets a singleton instance of <see cref="SecretKeyExpiresWhenComparer"/>.
    /// </summary>
    public static SecretKeyExpiresWhenComparer Singleton { get; } = new();

    private SecretKeyExpiresWhenComparer()
    {
        // nothing
    }

    /// <inheritdoc />
    public int Compare(SecretKey? x, SecretKey? y)
    {
        if (ReferenceEquals(x, y)) return 0;

        var xExpiresWhen = x?.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue;
        var yExpiresWhen = y?.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue;
        var result = xExpiresWhen.CompareTo(yExpiresWhen);
        if (result != 0) return result;

        var xHashCode = x?.GetHashCode() ?? 0;
        var yHashCode = y?.GetHashCode() ?? 0;
        return xHashCode.CompareTo(yHashCode);
    }
}
