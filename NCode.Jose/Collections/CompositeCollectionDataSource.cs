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
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that aggregates multiple <see cref="ICollectionDataSource{T}"/> instances.
/// </summary>
/// <remarks>
/// Because this class is intended to be used with DI, it does not own the <see cref="ICollectionDataSource{T}"/> instances.
/// Therefore, it does not dispose them. See the following for more information: https://stackoverflow.com/a/30287923/2502089
/// </remarks>
public class CompositeCollectionDataSource<T> : BaseDisposable, ICollectionDataSource<T>
{
    private object SyncObj { get; } = new();
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private List<IDisposable>? ChangeTokenRegistrations { get; set; }
    private IReadOnlyList<ICollectionDataSource<T>> DataSources { get; }
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
        DataSources = dataSources.ToList();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;

        List<IDisposable>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            disposables = [];

            if (ChangeTokenRegistrations is { Count: > 0 })
                disposables.AddRange(ChangeTokenRegistrations);

            if (ChangeTokenSource is not null)
                disposables.Add(ChangeTokenSource);

            CollectionOrNull = null;
            ChangeTokenRegistrations = null;
            ChangeTokenSource = null;
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
        ChangeTokenRegistrations ??= [];
        ChangeTokenRegistrations.AddRange(DataSources.Select(dataSource =>
            ChangeToken.OnChange(dataSource.GetChangeToken, HandleChange)));
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
        CollectionOrNull = DataSources.Count switch
        {
            0 => Enumerable.Empty<T>(),
            1 => DataSources[0].Collection,
            _ => DataSources.SelectMany(dataSource => dataSource.Collection)
        };
    }
}
