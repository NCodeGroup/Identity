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

using Microsoft.Extensions.Primitives;
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;

namespace NCode.Jose.Collections;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that uses a static collection of <typeparamref name="T"/> instances.
/// </summary>
public sealed class StaticCollectionDataSource<T> : ICollectionDataSource<T>
{
    /// <inheritdoc />
    public IEnumerable<T> Collection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="collection"/> class.
    /// </summary>
    /// <param name="collection">The collection of <typeparamref name="T"/> instances.</param>
    public StaticCollectionDataSource(IEnumerable<T> collection)
    {
        Collection = collection.ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var disposableCollection = Collection as IEnumerable<IDisposable>;
        disposableCollection?.DisposeAll();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
}
