// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Loads the server's secret protector through <see cref="ServerSecretProtectorLoader"/> when the host starts, so that
/// the Data Protection key ring, which protects and unprotects synchronously, finds it ready with every retired server
/// key a live ring key names. It must start before the hosted service that loads the key ring.
/// </summary>
/// <remarks>
/// It also warns when a Production host protects its master keys under a root key that is a local file.
/// </remarks>
// See ADR-1235 and ADR-1407.
internal class ServerSecretProtectorWarmup(
    ServerSecretProtectorLoader loader,
    IOptions<SecretProtectionOptions> protectionOptions,
    IHostEnvironment hostEnvironment,
    ILogger<ServerSecretProtectorWarmup> logger
) : IHostedService
{
    private ServerSecretProtectorLoader Loader { get; } = loader;

    private IOptions<SecretProtectionOptions> ProtectionOptionsAccessor { get; } =
        protectionOptions;

    private IHostEnvironment HostEnvironment { get; } = hostEnvironment;

    private ILogger<ServerSecretProtectorWarmup> Logger { get; } = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Loader.LoadAsync(cancellationToken);

        if (
            HostEnvironment.IsProduction()
            && string.Equals(
                ProtectionOptionsAccessor.Value.RootKeyProvider,
                KeyWrapperNames.Local,
                StringComparison.Ordinal
            )
        )
        {
            Logger.LocalRootKeyInProduction();
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
