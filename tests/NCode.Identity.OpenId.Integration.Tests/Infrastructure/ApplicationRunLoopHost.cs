// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// An <see cref="IHost"/> whose entry point awaits <c>RunAsync</c>, which stops the host when the application stops and
/// then disposes it. This host stops it that way, once, and leaves disposal to the run loop. Stopping it from the factory
/// as well stops every hosted service a second time, concurrently, and races that disposal.
/// </summary>
/// <param name="host">The started host that the entry point runs.</param>
internal sealed class ApplicationRunLoopHost(IHost host) : IHost
{
    private IHost InnerHost { get; } = host;

    private IHostApplicationLifetime Lifetime { get; } =
        host.Services.GetRequiredService<IHostApplicationLifetime>();

    /// <inheritdoc />
    public IServiceProvider Services => InnerHost.Services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        InnerHost.StartAsync(cancellationToken);

    // This method has no await on purpose: WebApplicationFactory.Dispose() blocks on the returned task, and an await
    // would capture xUnit's synchronization context.
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Lifetime.ApplicationStopped.Register(() => stopped.TrySetResult());
        Lifetime.StopApplication();
        return stopped.Task.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The run loop disposes the host once it has stopped it.
    }
}
