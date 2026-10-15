#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

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
