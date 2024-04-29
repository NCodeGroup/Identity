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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;
using NCode.Disposables;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that periodically polls for changes to the underlying data source.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public sealed class PeriodicPollingCollectionDataSource<T> : ICollectionDataSource<T>, IAsyncDisposable
{
    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private CancellationChangeToken? ChangeToken { get; set; }

    private IEnumerable<T> CurrentCollection { get; set; }
    private Func<CancellationToken, ValueTask<IEnumerable<T>?>> GetCollectionAsync { get; }

    private CancellationTokenSource PeriodicCancellationSource { get; } = new();
    private PeriodicTimer PeriodicTimer { get; }
    private Task PeriodicTimerTask { get; }

    /// <inheritdoc />
    public IEnumerable<T> Collection
    {
        get
        {
            ThrowIfDisposed();
            return CurrentCollection;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicPollingCollectionDataSource{T}"/> class.
    /// </summary>
    public PeriodicPollingCollectionDataSource(
        IEnumerable<T> initialCollection,
        Func<CancellationToken, ValueTask<IEnumerable<T>?>> getCollectionAsync,
        TimeSpan refreshInterval)
    {
        CurrentCollection = initialCollection;
        GetCollectionAsync = getCollectionAsync;

        PeriodicTimer = new PeriodicTimer(refreshInterval);
        PeriodicTimerTask = Task.Run(async () => await PeriodicTimerAsync());
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;

        List<IDisposable?>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            disposables =
            [
                PeriodicTimer,
                PeriodicTimerTask,
                PeriodicCancellationSource,
                ChangeTokenSource
            ];

            ChangeTokenSource = null;
            ChangeToken = null;
        }

        await PeriodicCancellationSource.CancelAsync();
        await PeriodicTimerTask;

        disposables.DisposeAll();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        EnsureChangeTokenInitialized();
        return ChangeToken;
    }

    [MemberNotNull(nameof(ChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ChangeToken is not null) return;

            ThrowIfDisposed();
            RefreshChangeToken();
        }
    }

    [MemberNotNull(nameof(ChangeToken))]
    private void RefreshChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
    }

    private async ValueTask PeriodicTimerAsync()
    {
        var cancellationToken = PeriodicCancellationSource.Token;
        try
        {
            while (await PeriodicTimer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await RefreshCollectionAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // TODO: use some kind of exception handling policy
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch
        {
            // TODO: log exception
        }
    }

    // private async ValueTask<IReadOnlyCollection<SecretKey>> GetSecretKeysAsync(CancellationToken cancellationToken)
    // {
    //     await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
    //     var store = storeManager.GetStore<ITenantStore>();
    //
    //     var persistedTenant = await store.TryGetByTenantIdAsync(TenantId, cancellationToken);
    //
    //     var secretKeys = persistedTenant is not null ?
    //         SecretSerializer.DeserializeSecrets(persistedTenant.Secrets, out _) :
    //         Array.Empty<SecretKey>();
    //
    //     return secretKeys;
    // }

    private async ValueTask RefreshCollectionAsync(CancellationToken cancellationToken)
    {
        var collection = await GetCollectionAsync(cancellationToken);
        if (collection is null) return;
        await NotifyChangeAsync(collection);
    }

    private async ValueTask NotifyChangeAsync(IEnumerable<T> collection)
    {
        CancellationTokenSource? oldTokenSource;

        if (IsDisposed) return;
        lock (SyncObj)
        {
            if (IsDisposed) return;

            oldTokenSource = ChangeTokenSource;

            RefreshChangeToken();

            CurrentCollection = collection;
        }

        if (oldTokenSource is not null)
        {
            await oldTokenSource.CancelAsync();
            oldTokenSource.Dispose();
        }
    }
}
