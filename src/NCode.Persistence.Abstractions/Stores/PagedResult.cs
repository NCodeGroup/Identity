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
