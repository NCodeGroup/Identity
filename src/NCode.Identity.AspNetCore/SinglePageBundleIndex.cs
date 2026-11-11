// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Holds a parsed bundle index together with the version of the file that it was parsed from.
/// </summary>
/// <param name="LastWriteTimeUtc">The last-write time of the index file when it was read.</param>
/// <param name="Length">The length of the index file in bytes when it was read.</param>
/// <param name="Index">The parsed index, or <see langword="null"/> when the file does not parse.</param>
/// <typeparam name="TIndex">The type that the index file parses into.</typeparam>
internal sealed record SinglePageBundleIndex<TIndex>(
    DateTime LastWriteTimeUtc,
    long Length,
    TIndex? Index
)
    where TIndex : class;
