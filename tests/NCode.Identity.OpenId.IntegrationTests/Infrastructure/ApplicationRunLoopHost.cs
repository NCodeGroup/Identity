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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

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
