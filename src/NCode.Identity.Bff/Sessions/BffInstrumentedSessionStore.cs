#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics;
using NCode.Identity.Bff.Diagnostics;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Wraps the registered <see cref="IBffSessionStore"/>, the default one or an application's, so that every store
/// operation the BFF performs is timed and traced the same way. The BFF's components depend on this type, never on the
/// registered store directly.
/// </summary>
// See ADR-0197 and ADR-0298.
internal sealed class BffInstrumentedSessionStore(IBffSessionStore inner, BffMetrics metrics)
    : IBffSessionStore
{
    private readonly IBffSessionStore _inner = inner;
    private readonly BffMetrics _metrics = metrics;

    /// <inheritdoc />
    public ValueTask<bool> TryCreateAsync(
        BffSessionRecord record,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.CreateOperation,
            () => _inner.TryCreateAsync(record, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<BffSessionRecord?> GetAsync(
        string storeKey,
        CancellationToken cancellationToken
    ) => MeasureAsync(BffMetrics.GetOperation, () => _inner.GetAsync(storeKey, cancellationToken));

    /// <inheritdoc />
    public ValueTask<bool> TryReplaceAsync(
        BffSessionRecord record,
        long expectedVersion,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.ReplaceOperation,
            () => _inner.TryReplaceAsync(record, expectedVersion, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask RemoveAsync(
        string storeKey,
        BffSessionMetadata metadata,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.RemoveOperation,
            () => _inner.RemoveAsync(storeKey, metadata, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindBySessionIdAsync(
        string issuer,
        string sessionId,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindBySessionIdOperation,
            () => _inner.FindBySessionIdAsync(issuer, sessionId, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> FindBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.FindBySubjectOperation,
            () => _inner.FindBySubjectAsync(issuer, subject, cancellationToken)
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
                _inner.TryAddTransactionAsync(
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
            () => _inner.TryTakeTransactionAsync(transactionKey, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<bool> TryAcquireRefreshLeaseAsync(
        string leaseKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.AcquireRefreshLeaseOperation,
            () => _inner.TryAcquireRefreshLeaseAsync(leaseKey, expiresWhen, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask<bool> IsRefreshLeaseHeldAsync(
        string leaseKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.IsRefreshLeaseHeldOperation,
            () => _inner.IsRefreshLeaseHeldAsync(leaseKey, cancellationToken)
        );

    /// <inheritdoc />
    public ValueTask ReleaseRefreshLeaseAsync(
        string leaseKey,
        CancellationToken cancellationToken
    ) =>
        MeasureAsync(
            BffMetrics.ReleaseRefreshLeaseOperation,
            () => _inner.ReleaseRefreshLeaseAsync(leaseKey, cancellationToken)
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
            _metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception: null);
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
            _metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception: null);
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
        _metrics.SessionStoreOperationCompleted(operation, startTimestamp, exception);
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag(BffMetrics.ErrorTypeAttribute, exception.GetType().FullName);
    }
}
