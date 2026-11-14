// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Loads the platform's secret protector through <see cref="PlatformSecretProtectorLoader"/> when the host starts, so
/// that the Data Protection key ring, which protects and unprotects synchronously, finds it ready with every retired
/// platform master key a live ring key names. It must start before the hosted service that loads the key ring.
/// </summary>
/// <remarks>
/// It also warns when a deployment, a host without <c>LocalDevelopment:Enabled</c>, protects its master keys under a
/// root key that is a local file.
/// </remarks>
// See ADR-1235 and ADR-1407.
internal class PlatformSecretProtectorWarmup(
    PlatformSecretProtectorLoader loader,
    IOptions<SecretProtectionOptions> protectionOptions,
    LocalDevelopmentMode localDevelopment,
    ILogger<PlatformSecretProtectorWarmup> logger
) : IHostedService
{
    private PlatformSecretProtectorLoader Loader { get; } = loader;

    private IOptions<SecretProtectionOptions> ProtectionOptionsAccessor { get; } =
        protectionOptions;

    private LocalDevelopmentMode LocalDevelopment { get; } = localDevelopment;

    private ILogger<PlatformSecretProtectorWarmup> Logger { get; } = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Loader.LoadAsync(cancellationToken);

        if (
            !LocalDevelopment.Enabled
            && string.Equals(
                ProtectionOptionsAccessor.Value.RootKeyProvider,
                KeyWrapperNames.Local,
                StringComparison.Ordinal
            )
        )
        {
            Logger.LocalRootKeyInDeployment();
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
