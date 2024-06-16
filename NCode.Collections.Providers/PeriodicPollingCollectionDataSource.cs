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
using NCode.Disposables;

namespace NCode.Collections.Providers;

/// <summary>
/// Contains context used for calling the <see cref="PeriodicPollingCollectionDataSource{TItem,TState}.GetCollectionAsync"/> delegate.
/// </summary>
/// <param name="State">The state instance that was originally passed to the <see cref="PeriodicPollingCollectionDataSource{TItem,TState}"/> constructor.</param>
/// <param name="CurrentCollection">The current collection that was last returned by the <see cref="PeriodicPollingCollectionDataSource{TItem,TState}.GetCollectionAsync"/> delegate.</param>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
/// <typeparam name="TState">The type of the state parameter used during refresh calls.</typeparam>
[PublicAPI]
public readonly record struct PeriodicPollingCollectionContext<TItem, TState>(
    TState State,
    IReadOnlyCollection<TItem> CurrentCollection);

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that periodically polls for changes to the underlying data source.
/// </summary>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
/// <typeparam name="TState">The type of the state parameter used during refresh calls.</typeparam>
[PublicAPI]
public sealed class PeriodicPollingCollectionDataSource<TItem, TState>
    : ICollectionDataSource<TItem>, IAsyncDisposable
{
    /// <summary>
    /// Represents a method that is called to refresh the collection periodically.
    /// </summary>
    public delegate ValueTask<IReadOnlyCollection<TItem>?> GetCollectionAsyncDelegate(
        PeriodicPollingCollectionContext<TItem, TState> context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Represents a method that is called to handle exceptions that occur during the refresh.
    /// </summary>
    public delegate ValueTask HandleExceptionAsyncDelegate(
        ExceptionDispatchInfo exception,
        CancellationToken cancellationToken);

    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private CancellationChangeToken? ChangeToken { get; set; }

    private TState State { get; }
    private IReadOnlyCollection<TItem> CurrentCollection { get; set; }
    private GetCollectionAsyncDelegate GetCollectionAsync { get; }
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
    /// <param name="state">Contains the state instance that is passed to the <see cref="GetCollectionAsync"/> delegate.</param>
    /// <param name="initialCollection">The initial collection.</param>
    /// <param name="refreshInterval">The period of time between each refresh.</param>
    /// <param name="getCollectionAsync">The function to refresh the collection periodically.</param>
    /// <param name="handleExceptionAsync">The function to handle exceptions that occur during the refresh.
    /// If the exception is re-thrown, the periodic refresh will stop and no further updates will be made.
    /// The default behavior is to re-throw the exception.
    /// </param>
    public PeriodicPollingCollectionDataSource(
        TState state,
        IReadOnlyCollection<TItem> initialCollection,
        TimeSpan refreshInterval,
        GetCollectionAsyncDelegate getCollectionAsync,
        HandleExceptionAsyncDelegate? handleExceptionAsync = default)
    {
        State = state;
        CurrentCollection = initialCollection;
        GetCollectionAsync = getCollectionAsync;
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

    private async ValueTask RefreshCollectionAsync(CancellationToken cancellationToken)
    {
        var context = new PeriodicPollingCollectionContext<TItem, TState>(State, CurrentCollection);
        var collection = await GetCollectionAsync(context, cancellationToken);
        if (collection is null) return;
        await NotifyChangeAsync(collection);
    }

    private async ValueTask NotifyChangeAsync(IReadOnlyCollection<TItem> collection)
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
