﻿#region Copyright Preamble

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

namespace NCode.Collections.Providers;

/// <summary>
/// Provides the composition root (i.e. top-level collection) of <typeparamref name="TItem"/> instances by
/// aggregating multiple data sources and providing change notifications.
/// </summary>
[PublicAPI]
public interface ICollectionProvider<out TItem, out TCollection> : ISupportChangeToken, IAsyncDisposable
    where TCollection : IEnumerable<TItem>
{
    /// <summary>
    /// Gets a read-only collection of <typeparamref name="TItem"/> instances.
    /// </summary>
    TCollection Collection { get; }
}
