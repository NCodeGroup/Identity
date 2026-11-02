// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents a single page of REST resources returned by a collection <c>GET</c> endpoint, using keyset (cursor)
/// pagination.
/// </summary>
/// <typeparam name="T">The type of the REST resource on the page.</typeparam>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CollectionResource<T>
{
    /// <summary>
    /// Gets the resources on this page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets the opaque continuation token that fetches the next page when passed back as the <c>cursor</c> query
    /// parameter, or <c>null</c> when this is the last page.
    /// </summary>
    public string? ContinuationToken { get; init; }
}
