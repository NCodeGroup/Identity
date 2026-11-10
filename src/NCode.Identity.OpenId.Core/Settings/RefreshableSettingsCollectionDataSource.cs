// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> for the collection of <see cref="Setting"/>
/// instances that are persisted for the platform. The collection is read again on a timer and on demand through
/// <see cref="RefreshAsync"/>, one refresh at a time, so a change that was committed on any replica takes effect
/// without waiting for the next period.
/// </summary>
/// <remarks>
/// The change token is signaled after the collection is replaced, so a consumer that reacts to it reads the new
/// settings. A refresh that fails keeps the current collection; a failure on the timer is logged and a failure of an
/// on-demand refresh propagates to its caller.
/// </remarks>
// See ADR-1219.
internal sealed class RefreshableSettingsCollectionDataSource
    : IAsyncDisposableCollectionDataSource<Setting>
{
    private int _disposeStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshableSettingsCollectionDataSource"/> class.
    /// </summary>
    /// <param name="initialSettings">The settings that the collection holds until the first change.</param>
    /// <param name="loadChangedSettingsAsync">
    /// Reads the persisted settings and returns them when they differ from the current ones, or <see langword="null"/>
    /// when they are unchanged.
    /// </param>
    /// <param name="refreshInterval">The period of the fallback refresh timer.</param>
    /// <param name="timeProvider">The provider of the refresh timer.</param>
    /// <param name="logger">The logger.</param>
    public RefreshableSettingsCollectionDataSource(
        IReadOnlyCollection<Setting> initialSettings,
        Func<CancellationToken, ValueTask<IReadOnlyCollection<Setting>?>> loadChangedSettingsAsync,
        TimeSpan refreshInterval,
        TimeProvider timeProvider,
        ILogger<RefreshableSettingsCollectionDataSource> logger
    )
    {
        Collection = initialSettings;
        LoadChangedSettingsAsync = loadChangedSettingsAsync;
        Logger = logger;
        Timer = timeProvider.CreateTimer(_ => OnTimer(), null, refreshInterval, refreshInterval);
    }

    /// <inheritdoc />
    public IEnumerable<Setting> Collection { get; private set; }

    private Func<
        CancellationToken,
        ValueTask<IReadOnlyCollection<Setting>?>
    > LoadChangedSettingsAsync { get; }
    private ILogger<RefreshableSettingsCollectionDataSource> Logger { get; }
    private ITimer Timer { get; }

    // One refresh at a time; never disposed, so a late waiter never observes a disposed semaphore.
    private SemaphoreSlim RefreshGate { get; } = new(1, 1);
    private CancellationTokenSource Stopping { get; } = new();
    private Lock SyncObj { get; } = new();
    private CancellationTokenSource ChangeTokenSource { get; set; } = new();
    private bool IsDisposed { get; set; }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        lock (SyncObj)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return new CancellationChangeToken(ChangeTokenSource.Token);
        }
    }

    /// <summary>
    /// Reads the persisted settings again and, when they changed, replaces the collection and signals the change.
    /// Does nothing after the data source is disposed.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the refresh.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            if (IsDisposed)
                return;

            // The loader returns null when the settings are unchanged.
            var settings = await LoadChangedSettingsAsync(cancellationToken);
            if (settings is null)
                return;

            CancellationTokenSource previous;
            lock (SyncObj)
            {
                Collection = settings;
                previous = ChangeTokenSource;
                ChangeTokenSource = new CancellationTokenSource();
            }

            // Consumers are signaled only after the collection is replaced, so they read the new settings.
            await previous.CancelAsync();
            previous.Dispose();
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private void OnTimer() => _ = RefreshOnScheduleAsync(Stopping.Token);

    private async ValueTask RefreshOnScheduleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (Stopping.IsCancellationRequested)
        {
            // The data source is being disposed.
        }
        catch (Exception exception)
        {
            Logger.PlatformSettingsRefreshFailed(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        await Timer.DisposeAsync();
        await Stopping.CancelAsync();
        await RefreshGate.WaitAsync(CancellationToken.None);
        try
        {
            lock (SyncObj)
            {
                IsDisposed = true;
            }
            ChangeTokenSource.Dispose();
        }
        finally
        {
            RefreshGate.Release();
        }
        Stopping.Dispose();
    }
}
