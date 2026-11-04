// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
