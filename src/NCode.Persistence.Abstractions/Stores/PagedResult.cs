// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

/// <summary>
/// Represents a single page of items returned by a keyset-paginated store query.
/// </summary>
/// <typeparam name="T">The type of the persisted item.</typeparam>
[PublicAPI]
public sealed class PagedResult<T>
{
    /// <summary>
    /// Gets the items on this page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets the opaque cursor that fetches the next page when passed back to the store, or <c>null</c> when this is
    /// the last page.
    /// </summary>
    public required string? NextCursor { get; init; }
}
