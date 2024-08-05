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

using JetBrains.Annotations;

namespace NCode.Collections.Providers.PeriodicPolling;

/// <summary>
/// Contains contextual information for when a periodic polling collection is refreshed.
/// </summary>
/// <param name="State">The state instance that was originally passed to the periodic polling collection when it was created.
/// This can be used to maintain state between refresh calls. For example, this could be a reference to a service that is used
/// to fetch data from a remote source. This can be <c>null</c> if the periodic polling collection was created without a state
/// parameter, or if the state parameter was <c>null</c> when the periodic polling collection was created.</param>
/// <param name="CurrentCollection">The current collection that the periodic polling collection is managing.</param>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
/// <typeparam name="TState">The type of the state parameter used during refresh calls.</typeparam>
[PublicAPI]
public readonly record struct PeriodicPollingCollectionContext<TItem, TState>(
    TState State,
    IReadOnlyCollection<TItem> CurrentCollection);
