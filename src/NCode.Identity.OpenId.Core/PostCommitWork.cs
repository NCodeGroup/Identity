// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

/// <summary>
/// Runs a step that follows a committed change on its own token, bounded by <see cref="Timeout"/>, so the request's
/// token reaches none of it and a hung store cannot hold the response forever.
/// </summary>
// See ADR-1351.
internal static class PostCommitWork
{
    /// <summary>
    /// Gets the time each step that follows a committed change may take.
    /// </summary>
    internal static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs <paramref name="step"/> for <paramref name="state"/> on a token that is cancelled once
    /// <see cref="Timeout"/> has elapsed on <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> that measures the bound.</param>
    /// <param name="step">The step to run.</param>
    /// <param name="state">The state to pass to <paramref name="step"/>.</param>
    /// <typeparam name="TState">The type of the state.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal static async ValueTask RunAsync<TState>(
        TimeProvider timeProvider,
        Func<TState, CancellationToken, ValueTask> step,
        TState state
    )
    {
        using var timeoutSource = new CancellationTokenSource(Timeout, timeProvider);
        await step(state, timeoutSource.Token);
    }
}
