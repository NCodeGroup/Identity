// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> hosted service that starts the platform over with a new master key when its
/// master keys cannot be unwrapped, for example after the local root key changed.
/// </summary>
/// <remarks>
/// It runs in <see cref="IHostedLifecycleService.StartingAsync"/>, which precedes the
/// <see cref="IHostedService.StartAsync"/> of every hosted service, so it runs before the platform secret protector
/// warmup whatever the registration order. When the platform's master keys cannot be unwrapped, it removes them and
/// evicts the platform from the key ring; the warmup then creates a new master key. It also revokes every Data
/// Protection key, because each key of the ring is encrypted under the removed master keys and none can be read again.
/// Production never registers it: there, a master key that cannot be unwrapped signals a lost or changed root key, and
/// the host stops until the root key is restored.
/// </remarks>
// See ADR-1423.
internal sealed class DeveloperPlatformMasterKeyPruning(
    ISecretProtectorProvider secretProtectorProvider,
    DeveloperMasterKeyReset masterKeyReset,
    IKeyManager keyManager,
    TimeProvider timeProvider,
    ILogger<DeveloperPlatformMasterKeyPruning> logger
) : IHostedLifecycleService
{
    /// <summary>
    /// The reason recorded when the platform's Data Protection keys are revoked.
    /// </summary>
    internal const string RevocationReason =
        "developer key pruning replaced the platform's master keys, which cannot be unwrapped";

    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;

    private DeveloperMasterKeyReset MasterKeyReset { get; } = masterKeyReset;

    private IKeyManager KeyManager { get; } = keyManager;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DeveloperPlatformMasterKeyPruning> Logger { get; } = logger;

    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SecretProtectorProvider.GetProtectorAsync(
                SecretOwner.Platform,
                [],
                cancellationToken
            );
        }
        catch (CryptographicException exception)
        {
            await MasterKeyReset.ResetAsync(SecretOwner.Platform, cancellationToken);
            var now = TimeProvider.GetUtcNow();
            KeyManager.RevokeAllKeys(now, RevocationReason);
            Logger.DeveloperPlatformMasterKeysReplaced(exception);
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
