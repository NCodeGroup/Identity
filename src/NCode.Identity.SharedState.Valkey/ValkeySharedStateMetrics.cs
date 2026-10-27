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
using System.Diagnostics.Metrics;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Owns the instruments of the <see cref="ValkeyMetrics.MeterName"/> meter, created once through the
/// <see cref="IMeterFactory"/> so each service provider has its own; the adapters record through its methods and never
/// touch the meter directly.
/// </summary>
// See ADR-0197.
internal sealed class ValkeySharedStateMetrics
{
    internal const string OperationDurationName = "ncode.identity.shared_state.operation.duration";

    internal const string OperationAttribute = "ncode.identity.shared_state.operation";

    internal const string AdapterAttribute = "ncode.identity.shared_state.adapter";

    internal const string ErrorTypeAttribute = "error.type";

    internal const string Adapter = "valkey";

    internal const string CounterIncrementOperation = "counter.increment";

    internal const string CounterIncrementUntilExceededOperation =
        "counter.increment_until_exceeded";

    internal const string CounterGetOperation = "counter.get";

    internal const string CounterRemoveOperation = "counter.remove";

    internal const string KeySetTryAddOperation = "key_set.try_add";

    internal const string KeySetContainsOperation = "key_set.contains";

    internal const string ValueSetTryAddOperation = "value_set.try_add";

    internal const string ValueSetGetOperation = "value_set.get";

    internal const string ValueSetTryReplaceOperation = "value_set.try_replace";

    internal const string ValueSetTryTakeOperation = "value_set.try_take";

    internal const string ValueSetRemoveOperation = "value_set.remove";

    internal const string IndexSetAddMemberOperation = "index_set.add_member";

    internal const string IndexSetGetMembersOperation = "index_set.get_members";

    internal const string IndexSetRemoveMemberOperation = "index_set.remove_member";

    internal const string IndexSetRemoveOperation = "index_set.remove";

    internal const string BroadcastPublishOperation = "broadcast.publish";

    // A round trip to a nearby server takes well under a millisecond, so the buckets resolve the sub-millisecond range
    // and still bound a stalled call.
    private static IReadOnlyList<double> BucketBoundaries { get; } =
    [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5];

    private Histogram<double> OperationDuration { get; }

    public ValkeySharedStateMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ValkeyMetrics.MeterName);

        OperationDuration = meter.CreateHistogram(
            OperationDurationName,
            unit: "s",
            description: "The duration of a shared-state operation against its store.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = BucketBoundaries }
        );
    }

    /// <summary>
    /// Runs an operation against the store and records its duration, tagged with the exception's type when it throws.
    /// </summary>
    /// <typeparam name="TResult">The type of the value the operation returns.</typeparam>
    /// <param name="operation">The bounded operation name, such as <c>counter.increment</c>.</param>
    /// <param name="action">The operation to run and measure.</param>
    /// <returns>The value the operation returned. An exception the operation threw is rethrown.</returns>
    public async ValueTask<TResult> MeasureAsync<TResult>(
        string operation,
        Func<ValueTask<TResult>> action
    )
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await action();

            OperationCompleted(operation, startTimestamp, exception: null);
            return result;
        }
        catch (Exception exception)
        {
            OperationCompleted(operation, startTimestamp, exception);
            throw;
        }
    }

    /// <summary>
    /// Runs an operation against the store and records its duration, tagged with the exception's type when it throws.
    /// </summary>
    /// <param name="operation">The bounded operation name, such as <c>broadcast.publish</c>.</param>
    /// <param name="action">The operation to run and measure.</param>
    /// <returns>A task that completes when the operation has completed. An exception it threw is rethrown.</returns>
    public async ValueTask MeasureAsync(string operation, Func<ValueTask> action)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await action();

            OperationCompleted(operation, startTimestamp, exception: null);
        }
        catch (Exception exception)
        {
            OperationCompleted(operation, startTimestamp, exception);
            throw;
        }
    }

    /// <summary>
    /// Records the duration of an operation that started at <paramref name="startTimestamp"/>.
    /// </summary>
    /// <param name="operation">The bounded operation name, such as <c>counter.increment</c>.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> at which the operation started.</param>
    /// <param name="exception">The exception the operation threw, or <see langword="null"/> when it succeeded.</param>
    private void OperationCompleted(string operation, long startTimestamp, Exception? exception)
    {
        if (!OperationDuration.Enabled)
            return;

        var tags = new TagList { { OperationAttribute, operation }, { AdapterAttribute, Adapter } };
        if (exception is not null)
            tags.Add(ErrorTypeAttribute, exception.GetType().FullName);

        OperationDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
    }
}
