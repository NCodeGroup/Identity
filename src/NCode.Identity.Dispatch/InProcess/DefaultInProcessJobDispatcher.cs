// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Provides the default, in-process, self-scheduling implementation of <see cref="IJobDispatcher"/>. Dispatched jobs
/// are buffered in an in-memory queue and executed by background workers, up to the configured number at once, with
/// retry and back-off, surviving transient handler failures but not a process crash. Replace this adapter (register a
/// different <see cref="IJobDispatcher"/> before calling <c>AddInProcessJobDispatcher</c>) with a durable or
/// distributed backend when at-least-once delivery across restarts is required.
/// </summary>
internal sealed class DefaultInProcessJobDispatcher(
    InProcessJobQueue queue,
    IHostApplicationLifetime applicationLifetime
) : IJobDispatcher
{
    private InProcessJobQueue Queue { get; } = queue;
    private IHostApplicationLifetime ApplicationLifetime { get; } = applicationLifetime;

    /// <inheritdoc />
    public ValueTask DispatchAsync<TJob>(
        TJob job,
        DispatchOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where TJob : class, IDispatchJob
    {
        ArgumentNullException.ThrowIfNull(job);

        var resolvedOptions = options ?? DispatchOptions.Default;
        var item = new DispatchWorkItem
        {
            DispatchId = Guid.NewGuid(),
            Job = job,
            Options = resolvedOptions,
            Attempt = 1,
        };

        if (resolvedOptions.Delay is { } delay && delay > TimeSpan.Zero)
        {
            // Fire-and-forget: the delayed enqueue is bounded by the application-stopping token.
            _ = Queue
                .EnqueueAfterAsync(item, delay, ApplicationLifetime.ApplicationStopping)
                .AsTask();
        }
        else
        {
            Queue.Enqueue(item);
        }

        return ValueTask.CompletedTask;
    }
}
