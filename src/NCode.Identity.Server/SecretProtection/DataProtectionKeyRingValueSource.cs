// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Reports whether the Data Protection key ring still needs a retired master key of the server. Data Protection stores
/// key elements and never rewrites one, so the ring's keys are not re-encrypted and there is nothing to rewrap: the
/// retired key is kept while a live ring key names it.
/// </summary>
/// <remarks>
/// A key ring that cannot be read is treated as naming every key, so the server's master key is then never destroyed
/// automatically.
/// </remarks>
// See ADR-1407.
internal sealed class DataProtectionKeyRingValueSource(
    ServerSecretProtectorLoader loader,
    DataProtectionKeyRingReader keyRingReader,
    ISecretProtectorProvider provider,
    IOptions<SecretProtectionOptions> protectionOptions,
    TimeProvider timeProvider
) : IProtectedValueSource
{
    private ServerSecretProtectorLoader Loader { get; } = loader;

    private DataProtectionKeyRingReader KeyRingReader { get; } = keyRingReader;

    private ISecretProtectorProvider Provider { get; } = provider;

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public ValueTask<int> RewrapAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(0);

    /// <inheritdoc />
    public ValueTask<bool> ReferencesAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    )
    {
        if (!IsServerOwner(owner))
            return ValueTask.FromResult(false);

        if (!KeyRingReader.TryRead(out var values))
            return ValueTask.FromResult(true);

        var now = TimeProvider.GetUtcNow();
        var grace = ProtectionOptions.Value.MasterKeys.RetirementGrace;

        return ValueTask.FromResult(
            values.Any(value =>
                value.IsLive(now, grace)
                && Provider.TryReadMasterKeyId(value.ProtectedValue, out var keyId)
                && keyId == masterKeyId
            )
        );
    }

    // Owners are matched without regard to case, as the key ring matches them.
    private bool IsServerOwner(SecretOwner owner)
    {
        var server = Loader.Owner;

        return string.Equals(owner.OwnerType, server.OwnerType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(owner.OwnerId, server.OwnerId, StringComparison.OrdinalIgnoreCase);
    }
}
