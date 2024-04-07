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
using Microsoft.Extensions.Primitives;
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;

namespace NCode.Jose.Collections;

/// <summary>
/// Provides a default implementation for the <see cref="ICollectionProvider{TItem,TCollection}"/> interface.
/// </summary>
public abstract class CollectionProvider<TItem, TCollection> : BaseDisposable, ICollectionProvider<TItem, TCollection>
    where TCollection : IEnumerable<TItem>
{
    private object SyncObj { get; } = new();
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private IDisposable? ChangeTokenRegistration { get; set; }
    private CompositeCollectionDataSource<TItem> DataSource { get; }
    private TCollection? CollectionOrNull { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionProvider{TItem,TCollection}"/> class with the specified collection of <see cref="ICollectionDataSource{T}"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{T}"/> instances to aggregate.</param>
    protected CollectionProvider(IEnumerable<ICollectionDataSource<TItem>> dataSources)
    {
        DataSource = new CompositeCollectionDataSource<TItem>(dataSources);
    }

    /// <inheritdoc />
    public TCollection Collection
    {
        get
        {
            EnsureCollectionInitialized();
            return CollectionOrNull;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (IsDisposed || !disposing) return;

        List<IDisposable> disposables = [DataSource];

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            if (ChangeTokenRegistration is not null)
                disposables.Add(ChangeTokenRegistration);

            if (ChangeTokenSource is not null)
                disposables.Add(ChangeTokenSource);
        }

        disposables.DisposeAll();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        EnsureChangeTokenInitialized();
        return ConsumerChangeToken;
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

    private void HandleChange()
    {
        CancellationTokenSource? oldTokenSource;

        if (IsDisposed) return;
        lock (SyncObj)
        {
            if (IsDisposed) return;

            oldTokenSource = ChangeTokenSource;

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

        oldTokenSource?.Cancel();
        oldTokenSource?.Dispose();
    }

    private void SubscribeChangeTokenProducers()
    {
        ChangeTokenRegistration = ChangeToken.OnChange(DataSource.GetChangeToken, HandleChange);
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

    /// <summary>
    /// Factory method to create a new <typeparamref name="TCollection"/> instance.
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{T}"/> collection of items.</param>
    /// <returns>The newly created <typeparamref name="TCollection"/> instance.</returns>
    protected abstract TCollection CreateCollection(IEnumerable<TItem> items);
}
