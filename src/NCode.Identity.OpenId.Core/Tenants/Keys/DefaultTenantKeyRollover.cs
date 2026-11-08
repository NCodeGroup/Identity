// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyRollover"/>. A generated key is protected under the
/// tenant's master key, which is loaded only when a local key is generated, and carries the rollover as the manager of
/// its lifecycle; a key whose specification names a remote key provider is created in that provider's key management
/// service instead, and no master key is loaded for it. An activated, retired or extended key keeps its material and
/// changes only the instants of its lifecycle, which stay in order or the change is refused; a removed key leaves the
/// store and is reported in the result, and the rollover destroys nothing. Each change to a key is
/// applied to the key as the changes before it left it, so a plan that activates and extends one key keeps both.
/// Every change is described by a <see cref="TenantKeyAuditEvent"/> with the <c>system</c> actor, because the server
/// made it on its own, which the caller publishes once its unit of work has committed; the audit event is the one
/// record of the change, so nothing is logged for a change the commit may yet discard.
/// </summary>
// See ADR-1404.
internal sealed class DefaultTenantKeyRollover(
    ISecretGenerator secretGenerator,
    ISecretProtectorProvider secretProtectorProvider,
    ICryptoService cryptoService
) : ITenantKeyRollover
{
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private ICryptoService CryptoService { get; } = cryptoService;

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

        // The keys as the changes so far left them: a later change to a key starts from the earlier one, so an
        // extension written after an activation keeps the activation.
        var secrets = new Dictionary<string, PersistedSecret>(StringComparer.Ordinal);
        foreach (var secret in held)
        {
            secrets[secret.SecretId] = secret;
        }

        var auditEvents = new List<TenantKeyAuditEvent>();
        var removedSecrets = new List<PersistedSecret>();

        foreach (var removal in plan.Removals)
        {
            var removed = secrets[removal.SecretId];
            await tenantStore.RemoveSecretAsync(tenantId, removed.SecretId, cancellationToken);
            secrets.Remove(removed.SecretId);
            removedSecrets.Add(removed);
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    removed,
                    TenantKeyChanges.Removed,
                    TenantKeyChangeReasons.Expired,
                    plan.Now
                )
            );
        }

        foreach (var retirement in plan.Retirements)
        {
            var retired = WithLifecycle(
                secrets[retirement.Secret.SecretId],
                retirement.ActivatesWhen,
                retirement.RetiresWhen,
                retirement.ExpiresWhen
            );
            await tenantStore.UpdateSecretAsync(tenantId, retired, cancellationToken);
            secrets[retired.SecretId] = retired;
            auditEvents.Add(
                CreateAuditEvent(
                    tenantId,
                    retired,
                    TenantKeyChanges.Retired,
                    retirement.Reason,
                    plan.Now
                )
            );
        }

        foreach (var activation in plan.Activations)
        {
            var current = secrets[activation.Secret.SecretId];
            var activated = WithLifecycle(
                current,
                activation.ActivatesWhen,
                current.RetiresWhen,
                current.ExpiresWhen
            );
            await tenantStore.UpdateSecretAsync(tenantId, activated, cancellationToken);
            secrets[activated.SecretId] = activated;
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
            var current = secrets[extension.Secret.SecretId];
            var extended = WithLifecycle(
                current,
                current.ActivatesWhen,
                current.RetiresWhen,
                extension.ExpiresWhen
            );
            await tenantStore.UpdateSecretAsync(tenantId, extended, cancellationToken);
            secrets[extended.SecretId] = extended;
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

        // The tenant's master key is loaded only when a local key has to be generated, so a pass that generates
        // nothing, or only remote keys, never touches the master key ring.
        ISecretProtector? protector = null;
        foreach (var generation in plan.Generations)
        {
            PersistedSecret generated;
            if (generation.Specification.RemoteKeyProvider is { } provider)
            {
                generated = await GenerateRemoteAsync(
                    tenantId,
                    provider,
                    generation,
                    plan.Now,
                    cancellationToken
                );
            }
            else
            {
                protector ??= await SecretProtectorProvider.GetProtectorAsync(
                    SecretOwner.ForTenant(tenantId),
                    [],
                    cancellationToken
                );

                generated = Generate(protector, generation, plan.Now);
            }

            await tenantStore.AddSecretAsync(tenantId, generated, cancellationToken);
            secrets[generated.SecretId] = generated;
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
            RemovedSecrets = removedSecrets,
        };
    }

    // The key is created in the provider's key management service; the secret stores only its public half and a
    // reference to it, so no master key is involved.
    private async ValueTask<PersistedSecret> GenerateRemoteAsync(
        string tenantId,
        string provider,
        TenantKeyGeneration generation,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var specification = generation.Specification;
        return await SecretGenerator.GenerateRemoteSecretAsync(
            new GenerateRemoteSecretRequest
            {
                SecretId = CryptoService.GenerateResourceId(),
                Owner = SecretOwner.ForTenant(tenantId),
                Provider = provider,
                SecretType = specification.SecretType,
                KeySizeBits = specification.KeySizeBits,
                Curve = specification.Curve,
                Use = specification.Use,
                Algorithm = specification.Algorithm,
                CreatedWhen = now,
                ActivatesWhen = generation.ActivatesWhen,
                RetiresWhen = generation.RetiresWhen,
                ExpiresWhen = generation.ExpiresWhen,
                ManagedBy = SecretManagers.TenantKeyRollover,
            },
            cancellationToken
        );
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
        var changed = secret with
        {
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = expiresWhen,
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
            RemoteKeyProvider = GetRemoteKeyProvider(secret),
            ActivatesWhen = secret.ActivatesWhen,
            RetiresWhen = secret.RetiresWhen,
            ExpiresWhen = secret.ExpiresWhen,
        };

    // The provider is named by the secret's reference; a local key, and a reference that does not parse, name none.
    private static string? GetRemoteKeyProvider(PersistedSecret secret) =>
        SecretEncodings.IsRemote(secret.SecretEncoding)
        && RemoteKeyReference.TryParse(secret.EncodedValue, out var reference)
            ? reference.Provider
            : null;
}
