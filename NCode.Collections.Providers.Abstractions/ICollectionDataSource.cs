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
/// Provides a collection of <typeparamref name="T"/> instances and notifications when changes occur.
/// </summary>
/// <remarks>
/// The concrete implementations should be registered in DI using the <see cref="ICollectionDataSource{T}"/>
/// closed generic interface and can optionally implement <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>
/// to dispose of any resources.
/// </remarks>
[PublicAPI]
public interface ICollectionDataSource<out T> : ISupportChangeToken
{
    /// <summary>
    /// Gets a read-only collection of <typeparamref name="T"/> instances.
    /// </summary>
    IEnumerable<T> Collection { get; }
}

/// <summary>
/// Provides a collection of <typeparamref name="T"/> instances and notifications when changes occur.
/// </summary>
[PublicAPI]
public interface IDisposableCollectionDataSource<out T> : ICollectionDataSource<T>, IDisposable
{
    // nothing
}

/// <summary>
/// Provides a collection of <typeparamref name="T"/> instances and notifications when changes occur.
/// </summary>
[PublicAPI]
public interface IAsyncDisposableCollectionDataSource<out T> : ICollectionDataSource<T>, IAsyncDisposable
{
    // nothing
}
