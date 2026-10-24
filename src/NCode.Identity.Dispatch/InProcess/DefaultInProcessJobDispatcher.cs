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
            _ = Queue.EnqueueAfterAsync(item, delay, ApplicationLifetime.ApplicationStopping);
        }
        else
        {
            Queue.Enqueue(item);
        }

        return ValueTask.CompletedTask;
    }
}
