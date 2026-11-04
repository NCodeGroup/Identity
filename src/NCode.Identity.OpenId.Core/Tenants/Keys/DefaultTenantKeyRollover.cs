// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyRollover"/>. A generated key is protected under the
/// tenant's master key, which is loaded only when a key is generated; an activated or retired key keeps its material
/// and changes only the instants of its lifecycle; a removed key leaves the store. Every change is recorded as a
/// <see cref="TenantKeyAuditEvent"/> with the <c>system</c> actor, because the server made it on its own.
/// </summary>
// See ADR-1404.
internal sealed class DefaultTenantKeyRollover(
    ISecretGenerator secretGenerator,
    ISecretProtectorProvider secretProtectorProvider,
    ICryptoService cryptoService,
    IEventPublisher eventPublisher,
    ILogger<DefaultTenantKeyRollover> logger
) : ITenantKeyRollover
{
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private ILogger<DefaultTenantKeyRollover> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<PersistedSecret>> ApplyAsync(
        ITenantStore tenantStore,
        string tenantId,
        IReadOnlyCollection<PersistedSecret> held,
        TenantKeyRolloverPlan plan,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(tenantStore);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(plan);

        var secrets = new Dictionary<string, PersistedSecret>(StringComparer.Ordinal);
        foreach (var secret in held)
        {
            secrets[secret.SecretId] = secret;
        }

        foreach (var removal in plan.Removals)
        {
            await tenantStore.RemoveSecretAsync(tenantId, removal.SecretId, cancellationToken);
            secrets.Remove(removal.SecretId);
            Logger.TenantKeyRemoved(tenantId, removal.Algorithm, removal.SecretId);
            await RecordAsync(
                tenantId,
                removal,
                TenantKeyChanges.Removed,
                TenantKeyChangeReasons.Expired,
                plan.Now,
                cancellationToken
            );
        }

        foreach (var retirement in plan.Retirements)
        {
            var retired = WithLifecycle(
                retirement.Secret,
                retirement.Secret.ActivatesWhen,
                retirement.RetiresWhen,
                retirement.ExpiresWhen
            );
            await tenantStore.UpdateSecretAsync(tenantId, retired, cancellationToken);
            secrets[retired.SecretId] = retired;
            Logger.TenantKeyRetired(
                tenantId,
                retired.Algorithm,
                retired.SecretId,
                retired.ExpiresWhen
            );
            await RecordAsync(
                tenantId,
                retired,
                TenantKeyChanges.Retired,
                TenantKeyChangeReasons.UnusedAlgorithm,
                plan.Now,
                cancellationToken
            );
        }

        foreach (var activation in plan.Activations)
        {
            var activated = WithLifecycle(
                activation.Secret,
                activation.ActivatesWhen,
                activation.Secret.RetiresWhen,
                activation.Secret.ExpiresWhen
            );
            await tenantStore.UpdateSecretAsync(tenantId, activated, cancellationToken);
            secrets[activated.SecretId] = activated;
            Logger.TenantKeyActivated(tenantId, activated.Algorithm, activated.SecretId);
            await RecordAsync(
                tenantId,
                activated,
                TenantKeyChanges.Activated,
                TenantKeyChangeReasons.NoActiveKey,
                plan.Now,
                cancellationToken
            );
        }

        // The tenant's master key is loaded only when a key has to be generated, so a pass that generates nothing
        // never touches the master key ring.
        ISecretProtector? protector = null;
        foreach (var generation in plan.Generations)
        {
            protector ??= await SecretProtectorProvider.GetProtectorAsync(
                SecretOwner.ForTenant(tenantId),
                [],
                cancellationToken
            );

            var generated = Generate(protector, generation, plan.Now);
            await tenantStore.AddSecretAsync(tenantId, generated, cancellationToken);
            secrets[generated.SecretId] = generated;
            Logger.TenantKeyProvisioned(
                tenantId,
                generation.Specification.Algorithm,
                generated.SecretId,
                generated.ActivatesWhen,
                generated.RetiresWhen
            );
            await RecordAsync(
                tenantId,
                generated,
                TenantKeyChanges.Generated,
                generation.Reason,
                plan.Now,
                cancellationToken
            );
        }

        return [.. secrets.Values];
    }

    private PersistedSecret Generate(
        ISecretProtector protector,
        TenantKeyGeneration generation,
        DateTimeOffset now
    )
    {
        var specification = generation.Specification;
        return SecretGenerator
            .GenerateSecret(
                protector,
                new GenerateSecretRequest
                {
                    SecretId = CryptoService.GenerateResourceId(),
                    SecretType = specification.SecretType,
                    GenerationParameters = new SecretKeyGenerationParameters
                    {
                        KeySizeBits = specification.KeySizeBits,
                        Curve = specification.Curve,
                    },
                    Use = specification.Use,
                    Algorithm = specification.Algorithm,
                    CreatedWhen = now,
                    ActivatesWhen = generation.ActivatesWhen,
                    RetiresWhen = generation.RetiresWhen,
                    ExpiresWhen = generation.ExpiresWhen,
                }
            )
            .Secret;
    }

    // Only the instants of the lifecycle change; the key's material and identity stay what they are.
    private static PersistedSecret WithLifecycle(
        PersistedSecret secret,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset expiresWhen
    ) =>
        new()
        {
            SecretId = secret.SecretId,
            KeyId = secret.KeyId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = expiresWhen,
            SecretType = secret.SecretType,
            SecretEncoding = secret.SecretEncoding,
            Origin = secret.Origin,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
            PublicKey = secret.PublicKey,
        };

    private ValueTask RecordAsync(
        string tenantId,
        PersistedSecret secret,
        string change,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new TenantKeyAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = tenantId,
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = change,
            Reason = reason,
            SecretId = secret.SecretId,
            KeyId = secret.KeyId ?? secret.SecretId,
            SecretType = secret.SecretType,
            Algorithm = secret.Algorithm,
            Use = secret.Use,
            ActivatesWhen = secret.ActivatesWhen,
            RetiresWhen = secret.RetiresWhen,
            ExpiresWhen = secret.ExpiresWhen,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }
}
