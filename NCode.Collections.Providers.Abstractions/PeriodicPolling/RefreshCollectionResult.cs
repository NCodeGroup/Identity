#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Collections.Providers.PeriodicPolling;

/// <summary>
/// Represents the result of a collection refresh operation.
/// </summary>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
[PublicAPI]
public readonly struct RefreshCollectionResult<TItem>
{
    /// <summary>
    /// Gets a value indicating whether the collection has changed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(NewCollection))]
    public bool IsChanged => NewCollection != null;

    /// <summary>
    /// Gets a value indicating whether the collection has not changed.
    /// </summary>
    [MemberNotNullWhen(false, nameof(NewCollection))]
    public bool IsUnchanged => !IsChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshCollectionResult{TItem}"/> struct with a new collection.
    /// </summary>
    /// <param name="newCollection">The new collection.</param>
    public RefreshCollectionResult(IReadOnlyCollection<TItem> newCollection)
    {
        NewCollection = newCollection;
    }

    /// <summary>
    /// Gets the new collection if the collection has changed; otherwise, <c>null</c>.
    /// </summary>
    public IReadOnlyCollection<TItem>? NewCollection { get; }
}
