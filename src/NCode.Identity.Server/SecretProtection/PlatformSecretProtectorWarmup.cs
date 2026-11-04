// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Loads the <see cref="ISecretProtector"/> of the platform when the host starts, so that the Data Protection key ring,
/// which protects and unprotects synchronously, finds it ready. It must start before the hosted service that loads the
/// key ring.
/// </summary>
/// <remarks>
/// It also warns when a Production host protects its master keys under a root key that is a local file.
/// </remarks>
// See ADR-1235.
internal class PlatformSecretProtectorWarmup(
    ISecretProtectorProvider provider,
    IOptions<SecretProtectionOptions> protectionOptions,
    IHostEnvironment hostEnvironment,
    PlatformSecretProtectorHolder holder,
    ILogger<PlatformSecretProtectorWarmup> logger
) : IHostedService
{
    private ISecretProtectorProvider Provider { get; } = provider;

    private IOptions<SecretProtectionOptions> ProtectionOptionsAccessor { get; } =
        protectionOptions;

    private IHostEnvironment HostEnvironment { get; } = hostEnvironment;

    private PlatformSecretProtectorHolder Holder { get; } = holder;

    private ILogger<PlatformSecretProtectorWarmup> Logger { get; } = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Holder.Protector = await Provider.GetProtectorAsync(
            SecretOwner.Platform,
            [],
            cancellationToken
        );

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
