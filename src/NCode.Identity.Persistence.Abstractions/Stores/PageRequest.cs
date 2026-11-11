// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Persistence.Stores;

/// <summary>
/// Describes the pagination window for a keyset-paginated store query: the continuation cursor and the maximum number
/// of items to return. It pairs with <see cref="PagedResult{T}"/> on every <c>GetPageAsync</c> store method.
/// </summary>
/// <remarks>
/// This is a growable request: new pagination criteria (such as a sort direction) are added as optional init-only
/// properties so the store pagination surface can evolve without a breaking signature change.
/// </remarks>
[PublicAPI]
public readonly record struct PageRequest
{
    /// <summary>
    /// Gets the opaque cursor returned by a previous call that fetches the next page, or <c>null</c> to fetch the
    /// first page.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets the maximum number of items to return on the page.
    /// </summary>
    public required int Limit { get; init; }
}
