// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
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
/// tenant's master key, which is loaded only when a key is generated, and carries the rollover as the manager of its
/// lifecycle; an activated, retired or extended key keeps its material and changes only the instants of its
/// lifecycle, which stay in order or the change is refused; a removed key leaves the store. Every change is described
/// by a <see cref="TenantKeyAuditEvent"/> with the <c>system</c> actor, because the server made it on its own, which
/// the caller publishes once its unit of work has committed.
/// </summary>
// See ADR-1404.
internal sealed class DefaultTenantKeyRollover(
    ISecretGenerator secretGenerator,
    ISecretProtectorProvider secretProtectorProvider,
    ICryptoService cryptoService,
    ILogger<DefaultTenantKeyRollover> logger
) : ITenantKeyRollover
{
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private ILogger<DefaultTenantKeyRollover> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<TenantKeyRolloverResult> ApplyAsync(
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

        var auditEvents = new List<TenantKeyAuditEvent>();

        foreach (var removal in plan.Removals)
        {
            await tenantStore.RemoveSecretAsync(tenantId, removal.SecretId, cancellationToken);
            secrets.Remove(removal.SecretId);
            Logger.TenantKeyRemoved(tenantId, removal.Algorithm, removal.SecretId);
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    removal,
                    TenantKeyChanges.Removed,
                    TenantKeyChangeReasons.Expired,
                    plan.Now
                )
            );
        }

        foreach (var retirement in plan.Retirements)
        {
            var retired = WithLifecycle(
                retirement.Secret,
                retirement.ActivatesWhen,
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
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    retired,
                    TenantKeyChanges.Retired,
                    TenantKeyChangeReasons.UnusedAlgorithm,
                    plan.Now
                )
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
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    activated,
                    TenantKeyChanges.Activated,
                    TenantKeyChangeReasons.NoActiveKey,
                    plan.Now
                )
            );
        }

        foreach (var extension in plan.Extensions)
        {
            var extended = WithLifecycle(
                extension.Secret,
                extension.Secret.ActivatesWhen,
                extension.Secret.RetiresWhen,
                extension.ExpiresWhen
            );
            await tenantStore.UpdateSecretAsync(tenantId, extended, cancellationToken);
            secrets[extended.SecretId] = extended;
            Logger.TenantKeyExtended(
                tenantId,
                extended.Algorithm,
                extended.SecretId,
                extended.ExpiresWhen
            );
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    extended,
                    TenantKeyChanges.Extended,
                    TenantKeyChangeReasons.RetirementGraceRaised,
                    plan.Now
                )
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
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    generated,
                    TenantKeyChanges.Generated,
                    generation.Reason,
                    plan.Now
                )
            );
        }

        return new TenantKeyRolloverResult
        {
            Secrets = [.. secrets.Values],
            AuditEvents = auditEvents,
        };
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
                    ManagedBy = SecretManagers.TenantKeyRollover,
                }
            )
            .Secret;
    }

    // Only the instants of the lifecycle change; the key's material and identity stay what they are, and the instants
    // stay in order, as every path that writes a secret guarantees, or the plan is refused as a whole.
    private static PersistedSecret WithLifecycle(
        PersistedSecret secret,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset expiresWhen
    )
    {
        var changed = new PersistedSecret
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
            ManagedBy = secret.ManagedBy,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
            PublicKey = secret.PublicKey,
        };

        if (!SecretLifecycle.TryValidate(changed, out var error))
        {
            throw new InvalidOperationException(
                $"The rollover plan leaves the key '{secret.SecretId}' with its lifecycle out of order: {error}"
            );
        }

        return changed;
    }

    private static TenantKeyAuditEvent CreateAuditEvent(
        string tenantId,
        PersistedSecret secret,
        string change,
        string reason,
        DateTimeOffset now
    ) =>
        new()
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
}
