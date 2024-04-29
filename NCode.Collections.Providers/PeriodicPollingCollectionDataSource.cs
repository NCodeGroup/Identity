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
    private Func<ExceptionDispatchInfo, ValueTask> ExceptionHandler { get; }

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
    /// <param name="initialCollection">The initial collection.</param>
    /// <param name="refreshInterval">The period of time between each refresh.</param>
    /// <param name="getCollectionAsync">The function to refresh the collection periodically.</param>
    /// <param name="exceptionHandler">The function to handle exceptions that occur during the refresh.
    /// If the exception is re-thrown, the periodic refresh will stop and no further updates will be made.
    /// The default behavior is to re-throw the exception.
    /// </param>
    public PeriodicPollingCollectionDataSource(
        IEnumerable<T> initialCollection,
        TimeSpan refreshInterval,
        Func<CancellationToken, ValueTask<IEnumerable<T>?>> getCollectionAsync,
        Func<ExceptionDispatchInfo, ValueTask>? exceptionHandler = default)
    {
        CurrentCollection = initialCollection;
        GetCollectionAsync = getCollectionAsync;
        ExceptionHandler = exceptionHandler ?? DefaultExceptionHandler;

        PeriodicTimer = new PeriodicTimer(refreshInterval);
        PeriodicTimerTask = Task.Run(async () => await PeriodicTimerAsync());
    }

    private static ValueTask DefaultExceptionHandler(ExceptionDispatchInfo exception)
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
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    await ExceptionHandler(ExceptionDispatchInfo.Capture(exception));
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
