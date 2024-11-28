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
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;

namespace NCode.Collections.Providers.DataSources;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that periodically polls for changes to the underlying data source.
/// </summary>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
/// <typeparam name="TState">The type of the state parameter used during refresh calls.</typeparam>
[PublicAPI]
public sealed class PeriodicPollingCollectionDataSource<TItem, TState>
    : ICollectionDataSource<TItem>, IAsyncDisposable
{
    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private CancellationChangeToken? ConsumerChangeToken { get; set; }

    private TState State { get; }
    private IReadOnlyCollection<TItem> CurrentCollection { get; set; }
    private RefreshCollectionAsyncDelegate<TItem, TState> RefreshCollectionAsync { get; }
    private HandleExceptionAsyncDelegate HandleExceptionAsync { get; }

    private CancellationTokenSource PeriodicCancellationSource { get; } = new();
    private PeriodicTimer PeriodicTimer { get; }
    private Task PeriodicTimerTask { get; }

    /// <inheritdoc />
    public IEnumerable<TItem> Collection
    {
        get
        {
            ThrowIfDisposed();
            return CurrentCollection;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicPollingCollectionDataSource{TItem,TState}"/> class.
    /// </summary>
    /// <param name="state">Contains the state instance that is passed to the <see cref="RefreshCollectionAsync"/> delegate.</param>
    /// <param name="initialCollection">The initial collection.</param>
    /// <param name="refreshInterval">The period of time between each refresh.</param>
    /// <param name="refreshCollectionAsync">The function to refresh the collection periodically.</param>
    /// <param name="handleExceptionAsync">The function to handle exceptions that occur during the refresh.
    /// If the exception is re-thrown, the periodic refresh will stop and no further updates will be made.
    /// The default behavior is to re-throw the exception.
    /// </param>
    public PeriodicPollingCollectionDataSource(
        TState state,
        IReadOnlyCollection<TItem> initialCollection,
        TimeSpan refreshInterval,
        RefreshCollectionAsyncDelegate<TItem, TState> refreshCollectionAsync,
        HandleExceptionAsyncDelegate? handleExceptionAsync = default)
    {
        State = state;
        CurrentCollection = initialCollection;
        RefreshCollectionAsync = refreshCollectionAsync;
        HandleExceptionAsync = handleExceptionAsync ?? DefaultHandleExceptionAsync;

        PeriodicTimer = new PeriodicTimer(refreshInterval);
        PeriodicTimerTask = Task.Run(async () => await PeriodicTimerAsync());
    }

    private static ValueTask DefaultHandleExceptionAsync(
        ExceptionDispatchInfo exception,
        CancellationToken cancellationToken)
    {
        exception.Throw();
        return ValueTask.CompletedTask;
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

            CurrentCollection = Array.Empty<TItem>();

            ChangeTokenSource = null;
            ConsumerChangeToken = null;
        }

        await PeriodicCancellationSource.CancelAsync();
        await PeriodicTimerTask;

        disposables.DisposeAll();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        EnsureChangeTokenInitialized();
        return ConsumerChangeToken;
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ConsumerChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ConsumerChangeToken is not null) return;

            ThrowIfDisposed();
            RefreshChangeToken();
        }
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void RefreshChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
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
                    await InvokeRefreshCollectionAsync(cancellationToken);
                }
                catch (OperationCanceledException exception)
                    when (exception.CancellationToken == cancellationToken)
                {
                    return;
                }
                catch (Exception exception)
                {
                    await HandleExceptionAsync(
                        ExceptionDispatchInfo.Capture(exception),
                        cancellationToken);
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private async ValueTask InvokeRefreshCollectionAsync(CancellationToken cancellationToken)
    {
        var result = await RefreshCollectionAsync(State, CurrentCollection, cancellationToken);
        if (result.IsChanged)
        {
            await NotifyChangeAsync(result.NewCollection);
        }
    }

    private async ValueTask NotifyChangeAsync(IReadOnlyCollection<TItem> newCollection)
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

                RefreshChangeToken();

                CurrentCollection = newCollection;
            }

            if (oldTokenSource is not null)
            {
                await oldTokenSource.CancelAsync();
            }
        }
        finally
        {
            oldTokenSource?.Dispose();
        }
    }
}
