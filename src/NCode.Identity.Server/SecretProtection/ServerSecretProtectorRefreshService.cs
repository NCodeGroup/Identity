// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Loads the server's secret protector again every <see cref="MasterKeyOptions.RefreshInterval"/>, so that the holder
/// that encrypts new Data Protection keys follows a rotation and drops a destroyed key even when a broadcast was lost.
/// A refresh that fails is logged, the holder keeps its protector, and the next interval tries again.
/// </summary>
// See ADR-1407.
internal class ServerSecretProtectorRefreshService(
    ServerSecretProtectorLoader loader,
    IOptions<SecretProtectionOptions> protectionOptions,
    TimeProvider timeProvider,
    ILogger<ServerSecretProtectorRefreshService> logger
) : BackgroundService
{
    private ServerSecretProtectorLoader Loader { get; } = loader;

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<ServerSecretProtectorRefreshService> Logger { get; } = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(
                ProtectionOptions.Value.MasterKeys.RefreshInterval,
                TimeProvider
            );
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    /// Loads the server's protector once, logging a failure instead of throwing it.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Loader.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.ServerSecretProtectorRefreshFailed(exception);
        }
    }
}
