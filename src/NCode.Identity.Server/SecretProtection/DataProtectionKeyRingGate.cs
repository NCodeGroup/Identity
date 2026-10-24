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

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Refuses to start the host while a live Data Protection key cannot be decrypted with the master key of the server,
/// and revokes the keys that <see cref="DataProtectionKeyRingOptions.RevokedKeyIds"/> lists. It starts after
/// <see cref="ServerSecretProtectorWarmup"/>, which loads the protector that decrypts the keys, and before the hosted
/// service of Data Protection, which would otherwise load the key ring and fail on the first key it cannot read.
/// </summary>
/// <remarks>
/// <para>
/// A key is live when it is not revoked and expires after the current time. The gate reads the identity, the
/// revocation and the expiration of each key before it decrypts anything, so a key that is listed for revocation is
/// revoked without being decrypted, and a key that is revoked or expired is ignored however unreadable it is.
/// </para>
/// <para>
/// A live key that cannot be decrypted is logged as an error. In the Development environment the gate revokes it, the
/// same heal that development secret pruning applies to the master keys of tenants. In every other environment the gate
/// throws an <see cref="InvalidOperationException"/> that names every such key and the setting that revokes them, so
/// the host does not run with a key ring that it cannot read.
/// </para>
/// </remarks>
// See ADR-0247.
internal class DataProtectionKeyRingGate(
    IKeyManager keyManager,
    IOptions<DataProtectionKeyRingOptions> optionsAccessor,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider,
    ILogger<DataProtectionKeyRingGate> logger
) : IHostedService
{
    /// <summary>
    /// The reason recorded when a key is revoked because it is listed in the configuration.
    /// </summary>
    internal const string ConfiguredRevocationReason =
        "listed in DataProtectionKeyRing:RevokedKeyIds";

    /// <summary>
    /// The reason recorded when a key is revoked because the Development environment cannot decrypt it.
    /// </summary>
    internal const string DevelopmentRevocationReason =
        "the server's master key cannot decrypt it in the Development environment";

    private IKeyManager KeyManager { get; } = keyManager;

    private IOptions<DataProtectionKeyRingOptions> OptionsAccessor { get; } = optionsAccessor;

    private IHostEnvironment HostEnvironment { get; } = hostEnvironment;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DataProtectionKeyRingGate> Logger { get; } = logger;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A live key cannot be decrypted and the environment is not
    /// Development.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var now = TimeProvider.GetUtcNow();
        var revokedKeyIds = OptionsAccessor.Value.RevokedKeyIds;
        List<Guid> undecryptableKeyIds = [];

        foreach (var key in KeyManager.GetAllKeys())
        {
            var keyId = key.KeyId;
            var isRevoked = key.IsRevoked;
            var expirationDate = key.ExpirationDate;

            if (isRevoked || expirationDate <= now)
                continue;

            if (revokedKeyIds.Contains(keyId))
            {
                Revoke(keyId, ConfiguredRevocationReason);
                continue;
            }

            if (CanDecrypt(key, keyId))
                continue;

            if (HostEnvironment.IsDevelopment())
            {
                Revoke(keyId, DevelopmentRevocationReason);
            }
            else
            {
                undecryptableKeyIds.Add(keyId);
            }
        }

        if (undecryptableKeyIds.Count == 0)
            return Task.CompletedTask;

        var keyIds = string.Join(", ", undecryptableKeyIds);
        throw new InvalidOperationException(
            $"Refusing to start: the Data Protection key ring holds {undecryptableKeyIds.Count} live key(s) that "
                + $"the server's master key cannot decrypt: {keyIds}. Restore the master key that encrypted them, or "
                + "revoke them by listing their IDs under 'DataProtectionKeyRing:RevokedKeyIds' and start the host "
                + "again."
        );
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool CanDecrypt(IKey key, Guid keyId)
    {
        try
        {
            // The descriptor decrypts the key material on first use, which is where a foreign master key fails.
            _ = key.Descriptor;
            return true;
        }
        catch (Exception exception)
        {
            Logger.KeyRingKeyUndecryptable(exception, keyId);
            return false;
        }
    }

    private void Revoke(Guid keyId, string reason)
    {
        KeyManager.RevokeKey(keyId, reason);
        Logger.KeyRingKeyRevoked(keyId, reason);
    }
}
