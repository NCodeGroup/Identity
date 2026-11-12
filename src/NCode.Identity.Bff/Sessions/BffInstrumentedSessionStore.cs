// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NCode.Identity.Bff.Diagnostics;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Wraps the registered <see cref="IBffSessionStore"/>, the default one or an application's, so that every store
/// operation the BFF performs is timed and traced the same way. The BFF's components depend on this type, never on the
/// registered store directly.
/// </summary>
// See ADR-1197.
internal sealed class BffInstrumentedSessionStore(IBffSessionStore inner, BffMetrics metrics)
    : IBffSessionStore
{
    private IBffSessionStore Inner { get; } = inner;
    private BffMetrics Metrics { get; } = metrics;

    /// <inheritdoc />
    public ValueTask<bool> TryCreateAsync(
        BffSessionRecord record,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.CreateOperation,
            token => Inner.TryCreateAsync(record, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<BffSessionRecord?> GetAsync(
        string storeKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.GetOperation,
            token => Inner.GetAsync(storeKey, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<bool> TryReplaceAsync(
        BffSessionRecord record,
        long expectedVersion,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.ReplaceOperation,
            token => Inner.TryReplaceAsync(record, expectedVersion, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(
        string storeKey,
        BffSessionMetadata metadata,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.RemoveOperation,
            token => Inner.RemoveAsync(storeKey, metadata, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindBySessionIdAsync(
        string issuer,
        string sessionId,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindBySessionIdOperation,
            token => Inner.FindBySessionIdAsync(issuer, sessionId, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindBySubjectOperation,
            token => Inner.FindBySubjectAsync(issuer, subject, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<BffSessionClient>> FindClientsAsync(
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindClientsOperation,
            token => Inner.FindClientsAsync(token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindByClientAsync(
        BffSessionClient client,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindByClientOperation,
            token => Inner.FindByClientAsync(client, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<bool> TryAddTransactionAsync(
        string transactionKey,
        ReadOnlyMemory<byte> protectedTransaction,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.AddTransactionOperation,
            token =>
                Inner.TryAddTransactionAsync(
                    transactionKey,
                    protectedTransaction,
                    expiresWhen,
                    token
                ),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> TryTakeTransactionAsync(
        string transactionKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.TakeTransactionOperation,
            token => Inner.TryTakeTransactionAsync(transactionKey, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<BffRefreshLease?> TryAcquireRefreshLeaseAsync(
        string leaseKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.AcquireRefreshLeaseOperation,
            token => Inner.TryAcquireRefreshLeaseAsync(leaseKey, expiresWhen, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<bool> IsRefreshLeaseHeldAsync(
        string leaseKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.IsRefreshLeaseHeldOperation,
            token => Inner.IsRefreshLeaseHeldAsync(leaseKey, token),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask ReleaseRefreshLeaseAsync(
        BffRefreshLease lease,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.ReleaseRefreshLeaseOperation,
            token => Inner.ReleaseRefreshLeaseAsync(lease, token),
            cancellationToken
        );

    private async ValueTask<TResult> MeasureAsync<TResult>(
        string operation,
        Func<CancellationToken, ValueTask<TResult>> action,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartActivity(operation);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await action(cancellationToken);
            Metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception: null);
            return result;
        }
        catch (Exception exception)
        {
            Failed(activity, operation, startTimestamp, exception);
            throw;
        }
    }

    private async ValueTask MeasureAsync(
        string operation,
        Func<CancellationToken, ValueTask> action,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartActivity(operation);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await action(cancellationToken);
            Metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception: null);
        }
        catch (Exception exception)
        {
            Failed(activity, operation, startTimestamp, exception);
            throw;
        }
    }

    private static Activity? StartActivity(string operation)
    {
        var activity = BffActivities.Source.StartActivity(BffActivities.SessionStoreOperation);
        activity?.SetTag(BffMetrics.SessionStoreOperationAttribute, operation);
        return activity;
    }

    private void Failed(
        Activity? activity,
        string operation,
        long startTimestamp,
        Exception exception
    )
    {
        Metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception);
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag(BffMetrics.ErrorTypeAttribute, exception.GetType().FullName);
    }
}
