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

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.Logging;
using NCode.Identity.Dispatch.Options;

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// A bounded in-memory channel of pending <see cref="DispatchWorkItem"/>s that the in-process dispatcher writes to and
/// the <see cref="JobDispatchWorker"/>'s consumers drain. Its capacity and full-queue behavior are host-configurable via
/// <see cref="InProcessJobDispatcherOptions"/>; a slow drain never stalls the producing path, and durable
/// at-least-once delivery is the job of a durable dispatcher adapter. A full queue drops the job and logs it,
/// consistently across the immediate, initial-delay, and retry enqueue paths.
/// </summary>
internal sealed class InProcessJobQueue
{
    private Channel<DispatchWorkItem> Channel { get; }
    private ILogger<InProcessJobQueue> Logger { get; }

    public InProcessJobQueue(
        IOptions<InProcessJobDispatcherOptions> options,
        ILogger<InProcessJobQueue> logger
    )
    {
        Logger = logger;

        var value = options.Value;
        Channel = System.Threading.Channels.Channel.CreateBounded<DispatchWorkItem>(
            new BoundedChannelOptions(value.QueueCapacity)
            {
                FullMode = value.FullMode,
                SingleReader = false,
                SingleWriter = false,
            }
        );
    }

    /// <summary>
    /// Gets the reader the worker's consumers drain.
    /// </summary>
    public ChannelReader<DispatchWorkItem> Reader => Channel.Reader;

    /// <summary>
    /// Enqueues a work item for immediate processing. A full queue drops the job and logs it.
    /// </summary>
    public void Enqueue(DispatchWorkItem item)
    {
        if (!Channel.Writer.TryWrite(item))
            LogDropped(item);
    }

    /// <summary>
    /// Enqueues a work item after the supplied delay, used for an initial scheduled delay and for retry back-off. A
    /// cancellation (process shutdown) silently abandons the pending enqueue; a full queue drops the job and logs it.
    /// </summary>
    public async Task EnqueueAfterAsync(
        DispatchWorkItem item,
        TimeSpan delay,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!Channel.Writer.TryWrite(item))
            LogDropped(item);
    }

    private void LogDropped(DispatchWorkItem item)
    {
        var jobType = item.Job.GetType();
        Logger.JobDropped(jobType.FullName ?? jobType.Name);
    }
}
