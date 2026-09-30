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
