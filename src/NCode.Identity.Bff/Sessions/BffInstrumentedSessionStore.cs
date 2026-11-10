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
// See ADR-1197 and ADR-1298.
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
            () => Inner.TryCreateAsync(record, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<BffSessionRecord?> GetAsync(
        string storeKey,
        CancellationToken cancellationToken
    ) => MeasureAsync(BffMetrics.GetOperation, () => Inner.GetAsync(storeKey, cancellationToken));

    /// <inheritdoc />
    public ValueTask<bool> TryReplaceAsync(
        BffSessionRecord record,
        long expectedVersion,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.ReplaceOperation,
            () => Inner.TryReplaceAsync(record, expectedVersion, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(
        string storeKey,
        BffSessionMetadata metadata,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.RemoveOperation,
            () => Inner.RemoveAsync(storeKey, metadata, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindBySessionIdAsync(
        string issuer,
        string sessionId,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindBySessionIdOperation,
            () => Inner.FindBySessionIdAsync(issuer, sessionId, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindBySubjectOperation,
            () => Inner.FindBySubjectAsync(issuer, subject, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<BffSessionClient>> FindClientsAsync(
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindClientsOperation,
            () => Inner.FindClientsAsync(cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindByClientAsync(
        BffSessionClient client,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindByClientOperation,
            () => Inner.FindByClientAsync(client, cancellationToken)
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
            () =>
                Inner.TryAddTransactionAsync(
                    transactionKey,
                    protectedTransaction,
                    expiresWhen,
                    cancellationToken
                )
        );

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> TryTakeTransactionAsync(
        string transactionKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.TakeTransactionOperation,
            () => Inner.TryTakeTransactionAsync(transactionKey, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<BffRefreshLease?> TryAcquireRefreshLeaseAsync(
        string leaseKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.AcquireRefreshLeaseOperation,
            () => Inner.TryAcquireRefreshLeaseAsync(leaseKey, expiresWhen, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<bool> IsRefreshLeaseHeldAsync(
        string leaseKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.IsRefreshLeaseHeldOperation,
            () => Inner.IsRefreshLeaseHeldAsync(leaseKey, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask ReleaseRefreshLeaseAsync(
        BffRefreshLease lease,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.ReleaseRefreshLeaseOperation,
            () => Inner.ReleaseRefreshLeaseAsync(lease, cancellationToken)
        );

    private async ValueTask<TResult> MeasureAsync<TResult>(
        string operation,
        Func<ValueTask<TResult>> action
    )
    {
        using var activity = StartActivity(operation);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await action();
            Metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception: null);
            return result;
        }
        catch (Exception exception)
        {
            Failed(activity, operation, startTimestamp, exception);
            throw;
        }
    }

    private async ValueTask MeasureAsync(string operation, Func<ValueTask> action)
    {
        using var activity = StartActivity(operation);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await action();
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
