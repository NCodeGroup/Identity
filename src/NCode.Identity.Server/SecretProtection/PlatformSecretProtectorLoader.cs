// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Loads the platform's secret protector into the holder: the protector of the platform's active master key, which also
/// holds every retired master key that a live key of the Data Protection key ring names. The key ring encrypts new keys
/// under the active key and is never re-encrypted, so a retired key stays loaded as long as a live ring key needs it.
/// </summary>
/// <remarks>
/// A master key that a ring key names but that is missing or does not unwrap is logged and skipped, so the key ring
/// gate still finds that ring key undecryptable and reports or revokes it.
/// </remarks>
// See ADR-1407 and ADR-1247.
internal class PlatformSecretProtectorLoader(
    ISecretProtectorProvider provider,
    DataProtectionKeyRingReader keyRingReader,
    IOptions<SecretProtectionOptions> protectionOptions,
    TimeProvider timeProvider,
    PlatformSecretProtectorHolder holder,
    ILogger<PlatformSecretProtectorLoader> logger
)
{
    private ISecretProtectorProvider Provider { get; } = provider;

    private DataProtectionKeyRingReader KeyRingReader { get; } = keyRingReader;

    private IOptions<SecretProtectionOptions> ProtectionOptionsAccessor { get; } =
        protectionOptions;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private PlatformSecretProtectorHolder Holder { get; } = holder;

    private ILogger<PlatformSecretProtectorLoader> Logger { get; } = logger;

    /// <summary>
    /// Gets the owner of the platform's master keys.
    /// </summary>
    public SecretOwner Owner { get; } = SecretOwner.Platform;

    /// <summary>
    /// Determines whether an owner is the platform's, matching without regard to case as the key ring does.
    /// </summary>
    /// <param name="owner">The owner to check.</param>
    /// <returns><see langword="true"/> when the owner is the platform's.</returns>
    public bool IsOwner(SecretOwner owner)
    {
        var platform = Owner;

        return string.Equals(
                owner.OwnerType,
                platform.OwnerType,
                StringComparison.OrdinalIgnoreCase
            ) && string.Equals(owner.OwnerId, platform.OwnerId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads the platform's protector and publishes it to the holder.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    public async ValueTask LoadAsync(CancellationToken cancellationToken)
    {
        var owner = Owner;
        var loadable = await GetLoadableValuesAsync(owner, cancellationToken);

        Holder.Protector = await Provider.GetProtectorAsync(owner, loadable, cancellationToken);
    }

    // Picks one protected value of every master key that the live ring keys name and that loads.
    private async ValueTask<IReadOnlyCollection<string>> GetLoadableValuesAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        // A ring this code cannot read names no key that the loader can load; the startup gate still checks every
        // key through the key manager.
        if (!KeyRingReader.TryRead(out var values))
            return [];

        var now = TimeProvider.GetUtcNow();
        var grace = ProtectionOptionsAccessor.Value.MasterKeys.RetirementGrace;

        var firstValueOfKey = new Dictionary<Guid, string>();
        foreach (var value in values.Where(value => value.IsLive(now, grace)))
        {
            if (
                Provider.TryReadMasterKeyId(value.ProtectedValue, out var keyId)
                && !firstValueOfKey.ContainsKey(keyId)
            )
            {
                firstValueOfKey[keyId] = value.ProtectedValue;
            }
        }

        List<string> loadable = [];
        foreach (var (keyId, value) in firstValueOfKey)
        {
            try
            {
                await Provider.GetProtectorAsync(owner, [value], cancellationToken);
                loadable.Add(value);
            }
            catch (CryptographicException exception)
            {
                Logger.KeyRingMasterKeyNotLoaded(exception, keyId);
            }
        }

        return loadable;
    }
}
