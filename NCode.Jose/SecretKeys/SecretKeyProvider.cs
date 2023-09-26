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
using NCode.Jose.Internal;
using NCode.Jose.SecretKeys.DataSources;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides the composition root (i.e. top-level collection) of <see cref="SecretKey"/> instances by aggregating multiple
/// <see cref="ISecretKeyDataSource"/> instances and providing change notifications.
/// </summary>
public interface ISecretKeyProvider : IDisposable
{
    /// <summary>
    /// Gets a read-only collection of <see cref="SecretKey"/> instances.
    /// </summary>
    ISecretKeyCollection SecretKeys { get; }

    /// <summary>
    /// Gets a <see cref="IChangeToken"/> that provides notifications when changes occur.
    /// </summary>
    IChangeToken GetChangeToken();
}

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyProvider"/> interface.
/// </summary>
public class SecretKeyProvider : BaseDisposable, ISecretKeyProvider
{
    private object SyncObj { get; } = new();
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private IDisposable? ChangeTokenRegistration { get; set; }
    private CompositeSecretKeyDataSource DataSource { get; }
    private ISecretKeyCollection? Collection { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyProvider"/> class with the specified collection of <see cref="ISecretKeyDataSource"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ISecretKeyDataSource"/> instances to aggregate.</param>
    public SecretKeyProvider(IEnumerable<ISecretKeyDataSource> dataSources)
    {
        DataSource = new CompositeSecretKeyDataSource(dataSources);
    }

    /// <inheritdoc />
    public ISecretKeyCollection SecretKeys
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

        List<IDisposable> disposables = new() { DataSource };

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
        ChangeTokenRegistration = ChangeToken.OnChange(DataSource.GetChangeToken, HandleChange);
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
        Collection = new SecretKeyCollection(DataSource.SecretKeys, owns: false);
    }
}
