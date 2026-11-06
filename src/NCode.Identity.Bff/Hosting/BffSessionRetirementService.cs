// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Hosting;

/// <summary>
/// Reconciles the stored sessions with the frontends when the host starts and after every change.
/// </summary>
/// <remarks>
/// A pass that fails, or that leaves a frontend to another holder of its lease, is tried again after
/// <c>Bff:SessionRetirement:RetryDelay</c>; every change of the frontends starts a pass for the changed set at once.
/// </remarks>
// See ADR-1443.
internal sealed class BffSessionRetirementService(
    BffFrontendCatalog frontends,
    BffSessionRetirement retirement,
    TimeProvider timeProvider,
    ILogger<BffSessionRetirementService> logger
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var changed = frontends.GetChangeToken();
            var current = frontends.Current;
            var delay = Timeout.InfiniteTimeSpan;

            try
            {
                var complete = await retirement.RetireAsync(current, stoppingToken);
                if (!complete)
                    delay = current.Options.SessionRetirement.RetryDelay;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.FrontendRetirementFailed(exception);
                delay = current.Options.SessionRetirement.RetryDelay;
            }

            var signal = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            using var registration = changed.RegisterChangeCallback(
                static state => (state as TaskCompletionSource)?.TrySetResult(),
                signal
            );
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            try
            {
                await Task.WhenAny(signal.Task, Task.Delay(delay, timeProvider, waiting.Token));
            }
            finally
            {
                await waiting.CancelAsync();
            }
        }
    }
}
