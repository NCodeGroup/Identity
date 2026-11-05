// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Reports whether the Data Protection key ring still needs a retired master key of the platform. Data Protection stores
/// key elements and never rewrites one, so the ring's keys are not re-encrypted and there is nothing to rewrap: the
/// retired key is kept while a live ring key names it.
/// </summary>
/// <remarks>
/// A key ring that cannot be read is treated as naming every key, so the platform's master key is then never destroyed
/// automatically.
/// </remarks>
// See ADR-1407.
internal sealed class DataProtectionKeyRingValueSource(
    PlatformSecretProtectorLoader loader,
    DataProtectionKeyRingReader keyRingReader,
    ISecretProtectorProvider provider,
    IOptions<SecretProtectionOptions> protectionOptions,
    TimeProvider timeProvider
) : IProtectedValueSource
{
    private PlatformSecretProtectorLoader Loader { get; } = loader;

    private DataProtectionKeyRingReader KeyRingReader { get; } = keyRingReader;

    private ISecretProtectorProvider Provider { get; } = provider;

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public ValueTask<ProtectedValueRewrapResult> RewrapAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(ProtectedValueRewrapResult.None);

    /// <inheritdoc />
    public ValueTask<IReadOnlySet<Guid>> FindReferencedAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        CancellationToken cancellationToken
    )
    {
        if (!Loader.IsOwner(owner))
            return ValueTask.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        if (!KeyRingReader.TryRead(out var values))
            return ValueTask.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>(masterKeyIds));

        var now = TimeProvider.GetUtcNow();
        var grace = ProtectionOptions.Value.MasterKeys.RetirementGrace;

        var referenced = new HashSet<Guid>();
        foreach (var value in values)
        {
            if (
                value.IsLive(now, grace)
                && Provider.TryReadMasterKeyId(value.ProtectedValue, out var keyId)
                && masterKeyIds.Contains(keyId)
            )
            {
                referenced.Add(keyId);
            }
        }

        return ValueTask.FromResult<IReadOnlySet<Guid>>(referenced);
    }
}
