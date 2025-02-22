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
using NCode.Collections.Providers.DataSources;
using NCode.Disposables;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides a base implementation for the <see cref="ICollectionProvider{TItem,TCollection}"/> abstraction.
/// </summary>
[PublicAPI]
public abstract class BaseCollectionProvider<TItem, TCollection> :
    ICollectionProvider<TItem, TCollection>,
    ICollectionDataSource<TItem>
    where TCollection : IEnumerable<TItem>
{
    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private List<IDisposable>? ChangeTokenRegistrations { get; set; }

    private bool Owns { get; }
    private ICollectionDataSource<TItem> DataSource { get; }
    private IReadOnlyCollection<ISupportChangeToken> ChangeTokenProducers { get; }
    private TCollection? CollectionOrNull { get; set; }

    IEnumerable<TItem> ICollectionDataSource<TItem>.Collection => Collection;

    /// <inheritdoc />
    public TCollection Collection
    {
        get
        {
            EnsureCollectionInitialized();
            return CollectionOrNull;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCollectionProvider{TItem,TCollection}"/> class with the specified <see cref="ICollectionDataSource{T}"/> instance.
    /// This variant will own the data source and dispose of it when the provider itself is disposed.
    /// </summary>
    /// <param name="dataSource">The <see cref="ICollectionDataSource{T}"/> instance for the provider.</param>
    /// <param name="owns">Indicates whether the provider will own the data source and dispose of it
    /// when the provider itself is disposed. The default is <c>true</c>.</param>
    protected BaseCollectionProvider(
        ICollectionDataSource<TItem> dataSource,
        bool owns = true)
    {
        Owns = owns;
        DataSource = dataSource;
        ChangeTokenProducers = [dataSource];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCollectionProvider{TItem,TCollection}"/> class with the specified <see cref="ICollectionDataSource{T}"/> instance.
    /// This variant will own the data source and dispose of it when the provider itself is disposed.
    /// </summary>
    /// <param name="dataSource">The <see cref="ICollectionDataSource{T}"/> instance for the provider.</param>
    /// <param name="additionalChangeTokenProducers">A collection of <see cref="ISupportChangeToken"/> instances that additionally produce change notifications.</param>
    /// <param name="owns">Indicates whether the provider will own the data source and dispose of it
    /// when the provider itself is disposed. The default is <c>true</c>.</param>
    protected BaseCollectionProvider(
        ICollectionDataSource<TItem> dataSource,
        IEnumerable<ISupportChangeToken> additionalChangeTokenProducers,
        bool owns = true)
    {
        Owns = owns;
        DataSource = dataSource;
        ChangeTokenProducers = [dataSource, ..additionalChangeTokenProducers];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCollectionProvider{TItem,TCollection}"/> class with the specified collection of <see cref="ICollectionDataSource{T}"/> instances.
    /// By default, we do not own the individual data sources and therefore not dispose of them when the provider
    /// itself is disposed because the data sources are resolved from the DI container which will manage their lifetimes.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{T}"/> instances to aggregate.</param>
    /// <param name="owns">Indicates whether this instance will own the individual data sources and dispose of them
    /// when this class is disposed. The default is <c>false</c>.</param>
    protected BaseCollectionProvider(
        IEnumerable<ICollectionDataSource<TItem>> dataSources,
        bool owns = false)
    {
        var dataSource = new CompositeCollectionDataSource<TItem>(dataSources)
        {
            Owns = owns
        };

        Owns = true; // this variant always own the composite data source
        DataSource = dataSource;
        ChangeTokenProducers = [dataSource];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCollectionProvider{TItem,TCollection}"/> class with the specified collection of <see cref="ICollectionDataSource{T}"/> instances.
    /// By default, we do not own the individual data sources and therefore not dispose of them when the provider
    /// itself is disposed because the data sources are resolved from the DI container which will manage their lifetimes.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{T}"/> instances to aggregate.</param>
    /// <param name="additionalChangeTokenProducers">A collection of <see cref="ISupportChangeToken"/> instances that additionally produce change notifications.</param>
    /// <param name="owns">Indicates whether this instance will own the individual data sources and dispose of them
    /// when this class is disposed. The default is <c>false</c>.</param>
    protected BaseCollectionProvider(
        IEnumerable<ICollectionDataSource<TItem>> dataSources,
        IEnumerable<ISupportChangeToken> additionalChangeTokenProducers,
        bool owns = false)
    {
        var dataSource = new CompositeCollectionDataSource<TItem>(dataSources)
        {
            Owns = owns
        };

        Owns = true; // this variant always own the composite data source
        DataSource = dataSource;
        ChangeTokenProducers = [dataSource, ..additionalChangeTokenProducers];
    }

    /// <summary>
    /// Factory method to create a new <typeparamref name="TCollection"/> instance.
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{T}"/> collection of items.</param>
    /// <returns>The newly created <typeparamref name="TCollection"/> instance.</returns>
    protected abstract TCollection CreateCollection(IEnumerable<TItem> items);

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
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting managed resources asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (IsDisposed) return;

        List<object>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            disposables = [];

            if (Owns)
            {
                disposables.Add(DataSource);
            }

            if (ChangeTokenRegistrations is { Count: > 0 })
            {
                disposables.AddRange(ChangeTokenRegistrations);
            }

            if (ChangeTokenSource is not null)
            {
                disposables.Add(ChangeTokenSource);
            }

            CollectionOrNull = default;
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
        CancellationTokenSource? oldTokenSource;

        if (IsDisposed) return;
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

        oldTokenSource?.Dispose();
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
        CollectionOrNull = CreateCollection(DataSource.Collection);
    }
}
