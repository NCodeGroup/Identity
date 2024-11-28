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

namespace NCode.Collections.Providers.PeriodicPolling;

/// <summary>
/// Factory methods for creating instances of <see cref="RefreshCollectionResult{TItem}"/>.
/// </summary>
public static class RefreshCollectionResultFactory
{
    /// <summary>
    /// Factory method for creating an instance of <see cref="RefreshCollectionResult{TItem}"/> indicating that the collection has not changed.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    public static RefreshCollectionResult<TItem> Unchanged<TItem>() => default;

    /// <summary>
    /// Factory method for creating an instance of <see cref="RefreshCollectionResult{TItem}"/> indicating that the collection has changed.
    /// </summary>
    /// <param name="newCollection">The new collection.</param>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    public static RefreshCollectionResult<TItem> Changed<TItem>(IReadOnlyCollection<TItem> newCollection) => new(newCollection);
}
