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

using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IValkeyConnection"/>: one <see cref="ConnectionMultiplexer"/>,
/// connected on first use and shared by every caller. The configuration does not abort on a failed connect, so a
/// server that is unreachable when the process starts is retried in the background and a command fails until it is
/// reachable, rather than the process failing to start.
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "Connects to a live server; a passthrough to StackExchange.Redis."
)]
internal sealed class DefaultValkeyConnection(ConfigurationOptions configurationOptions)
    : IValkeyConnection,
        IAsyncDisposable
{
    private Lazy<Task<ConnectionMultiplexer>> Multiplexer { get; } =
        new(() => ConnectionMultiplexer.ConnectAsync(configurationOptions));

    /// <inheritdoc />
    public async ValueTask<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        var multiplexer = await Multiplexer.Value.WaitAsync(cancellationToken);
        return multiplexer.GetDatabase();
    }

    /// <inheritdoc />
    public async ValueTask<ISubscriber> GetSubscriberAsync(CancellationToken cancellationToken) =>
        (await Multiplexer.Value.WaitAsync(cancellationToken)).GetSubscriber();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!Multiplexer.IsValueCreated)
            return;

        // Waits out a connect still in flight; a connect that failed has nothing to dispose.
        var connecting = Multiplexer.Value;
        await ((Task)connecting).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!connecting.IsCompletedSuccessfully)
            return;

        var multiplexer = await connecting;
        await multiplexer.DisposeAsync();
    }
}
