﻿#region Copyright Preamble

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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides an implementation of <see cref="ISecretKeyDataSource"/> that aggregates multiple <see cref="ISecretKeyDataSource"/> instances.
/// </summary>
/// <remarks>
/// Because this class is intended to be used with DI, it does not own the <see cref="ISecretKeyDataSource"/> instances.
/// Therefore, it does not dispose them. See the following for more information: https://stackoverflow.com/a/30287923/2502089
/// </remarks>
public class CompositeSecretKeyDataSource : BaseDisposable, ISecretKeyDataSource
{
    private object SyncObj { get; } = new();
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private List<IDisposable>? ChangeTokenRegistrations { get; set; }
    private IReadOnlyList<ISecretKeyDataSource> DataSources { get; }
    private IEnumerable<SecretKey>? Collection { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSecretKeyDataSource"/> class with the specified collection of <see cref="ISecretKeyDataSource"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ISecretKeyDataSource"/> instances to aggregate.</param>
    public CompositeSecretKeyDataSource(IEnumerable<ISecretKeyDataSource> dataSources)
    {
        DataSources = dataSources.ToList();
    }

    /// <inheritdoc />
    public IEnumerable<SecretKey> SecretKeys
    {
        get
        {
            EnsureCollectionInitialized();
            return Collection;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (IsDisposed || !disposing) return;

        List<IDisposable> disposables = new();

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            if (ChangeTokenRegistrations is { Count: > 0 })
                disposables.AddRange(ChangeTokenRegistrations);

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

    [MemberNotNull(nameof(Collection))]
    private void EnsureCollectionInitialized()
    {
        if (Collection is not null) return;
        lock (SyncObj)
        {
            if (Collection is not null) return;

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
            if (Collection is not null)
            {
                RefreshCollection();
            }
        }

        oldTokenSource?.Cancel();
        oldTokenSource?.Dispose();
    }

    private void SubscribeChangeTokenProducers()
    {
        ChangeTokenRegistrations ??= new List<IDisposable>();
        ChangeTokenRegistrations.AddRange(DataSources.Select(dataSource =>
            ChangeToken.OnChange(dataSource.GetChangeToken, HandleChange)));
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void RefreshConsumerChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
    }

    [MemberNotNull(nameof(Collection))]
    private void RefreshCollection()
    {
        Collection = DataSources.Count == 1 ?
            DataSources[0].SecretKeys :
            DataSources.SelectMany(dataSource => dataSource.SecretKeys);
    }
}
