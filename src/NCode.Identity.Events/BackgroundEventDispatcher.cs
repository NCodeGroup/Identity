// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

namespace NCode.Identity.Events;

/// <summary>
/// Drains the <see cref="DefaultBackgroundEventQueue"/> and delivers each event to the handlers that
/// opted into background delivery, each in its own scope, using the application-lifetime token.
/// </summary>
internal sealed class BackgroundEventDispatcher(
    DefaultBackgroundEventQueue queue,
    DefaultEventPublisher publisher
) : BackgroundService
{
    private DefaultBackgroundEventQueue Queue { get; } = queue;
    private DefaultEventPublisher Publisher { get; } = publisher;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var @event in Queue.Reader.ReadAllAsync(stoppingToken))
            {
                // DispatchBackgroundAsync isolates each handler, so one failure cannot stop the drain.
                await Publisher.DispatchBackgroundAsync(@event, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }
}
