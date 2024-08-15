#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.Extensions.Primitives;
using NCode.Disposables;

namespace NCode.Collections.Providers.DataSources;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that aggregates multiple <see cref="ICollectionDataSource{T}"/> instances.
/// </summary>
/// <remarks>
/// Because this class is intended to be used with DI, it does not own the <see cref="ICollectionDataSource{T}"/> instances.
/// Therefore, it does not dispose them. See the following for more information: https://stackoverflow.com/a/30287923/2502089
/// </remarks>
[PublicAPI]
public sealed class CompositeCollectionDataSource<T> : ICollectionDataSource<T>, IAsyncDisposable
{
    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private List<IDisposable>? ChangeTokenRegistrations { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this collection owns the individual data sources and therefore disposes of
    /// them when this class is disposed. By default, we do not own the individual data sources and therefore not dispose
    /// of them when the composite collection itself is disposed because the data sources are usually resolved from the DI
    /// container which will manage their lifetimes.
    /// </summary>
    public bool Owns { get; init; }

    /// <summary>
    /// Gets or sets a function that is used to combine the items from the individual data sources.
    /// The default is to concatenate the items.
    /// </summary>
    public Func<IEnumerable<IEnumerable<T>>, IEnumerable<T>> CombineFunc { get; init; } = DefaultCombineFunc;

    private IReadOnlyList<ICollectionDataSource<T>> DataSources { get; }
    private IReadOnlyCollection<ISupportChangeToken> ChangeTokenProducers { get; }
    private IEnumerable<T>? CollectionOrNull { get; set; }

    /// <inheritdoc />
    public IEnumerable<T> Collection
    {
        get
        {
            EnsureCollectionInitialized();
            return CollectionOrNull;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCollectionDataSource{T}"/> class with the specified collection of <see cref="ICollectionDataSource{T}"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{T}"/> instances to aggregate.</param>
    public CompositeCollectionDataSource(IEnumerable<ICollectionDataSource<T>> dataSources)
    {
        var dataSourcesList = dataSources.ToList();

        DataSources = dataSourcesList;
        ChangeTokenProducers = dataSourcesList;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCollectionDataSource{T}"/> class with the specified collection of <see cref="ICollectionDataSource{T}"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{T}"/> instances to aggregate.</param>
    /// <param name="additionalChangeTokenProducers">A collection of <see cref="ISupportChangeToken"/> instances that additionally produce change notifications.</param>
    public CompositeCollectionDataSource(
        IEnumerable<ICollectionDataSource<T>> dataSources,
        IEnumerable<ISupportChangeToken> additionalChangeTokenProducers)
    {
        var dataSourcesList = dataSources.ToList();

        DataSources = dataSourcesList;
        ChangeTokenProducers = [..dataSourcesList, ..additionalChangeTokenProducers];
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        EnsureChangeTokenInitialized();
        return ConsumerChangeToken;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;

        List<IAsyncDisposable>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            disposables = [];

            if (Owns)
            {
                foreach (var dataSource in DataSources)
                {
                    switch (dataSource)
                    {
                        case IAsyncDisposable asyncDisposable:
                            disposables.Add(asyncDisposable);
                            break;

                        case IDisposable disposable:
                            disposables.Add(AsyncDisposable.Adapt(disposable));
                            break;
                    }
                }
            }

            if (ChangeTokenRegistrations is { Count: > 0 })
                disposables.AddRange(ChangeTokenRegistrations.Select(AsyncDisposable.Adapt));

            if (ChangeTokenSource is not null)
                disposables.Add(AsyncDisposable.Adapt(ChangeTokenSource));

            CollectionOrNull = null;
            ChangeTokenRegistrations = null;

            ChangeTokenSource = null;
            ConsumerChangeToken = null;
        }

        await disposables.DisposeAllAsync();
    }

    [MemberNotNull(nameof(CollectionOrNull))]
    private void EnsureCollectionInitialized()
    {
        if (CollectionOrNull is not null) return;
        lock (SyncObj)
        {
            if (CollectionOrNull is not null) return;

            ThrowIfDisposed();
            EnsureChangeTokenInitialized();
            RefreshCollection();
        }
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ConsumerChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ConsumerChangeToken is not null) return;

            ThrowIfDisposed();
            SubscribeChangeTokenProducers();
            RefreshConsumerChangeToken();
        }
    }

    private void SubscribeChangeTokenProducers()
    {
        ChangeTokenRegistrations ??= [];
        ChangeTokenRegistrations.AddRange(ChangeTokenProducers.Select(changeTokenProducer =>
            ChangeToken.OnChange(changeTokenProducer.GetChangeToken, HandleChange)));
    }

    private void HandleChange()
    {
        if (IsDisposed) return;

        CancellationTokenSource? oldTokenSource = null;
        try
        {
            lock (SyncObj)
            {
                if (IsDisposed) return;

                oldTokenSource = ChangeTokenSource;

                ChangeTokenSource = null;
                ConsumerChangeToken = null;

                // refresh the cached change token
                if (oldTokenSource is not null)
                {
                    RefreshConsumerChangeToken();
                }

                // refresh the cached collection
                if (CollectionOrNull is not null)
                {
                    RefreshCollection();
                }
            }

            // this will trigger the consumer change token
            // and most importantly after the collection has been refreshed
            oldTokenSource?.Cancel();
        }
        finally
        {
            oldTokenSource?.Dispose();
        }
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void RefreshConsumerChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
    }

    [MemberNotNull(nameof(CollectionOrNull))]
    private void RefreshCollection()
    {
        CollectionOrNull = CombineFunc(DataSources.Select(dataSource => dataSource.Collection));
    }

    private static IEnumerable<T> DefaultCombineFunc(IEnumerable<IEnumerable<T>> collections) =>
        collections.SelectMany(collection => collection);
}
