// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyRolloverPlanner"/>. For every provisioned key the
/// tenant's settings use, exactly one held key signs or encrypts at a time: when none does, a published key activates
/// at once or a key is generated that activates at once; when the last key that signs or encrypts retires within the
/// activation delay plus the pass interval, its successor is generated, published from now and activating at that
/// retirement, so a verifier holds it for the whole delay before the first token it signs. A key the server
/// generated for an algorithm the tenant's settings no longer use retires now and expires at the end of the
/// retirement grace, and an expired key the server generated is removed; a key an operator supplied is never retired
/// or removed.
/// </summary>
// See ADR-1404.
internal sealed class DefaultTenantKeyRolloverPlanner(
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy
) : ITenantKeyRolloverPlanner
{
    private OpenIdTenantOptions Options { get; } = optionsAccessor.Value.Tenant;
    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;

    private static bool IsGenerated(PersistedSecret secret) =>
        string.Equals(secret.Origin, SecretOrigins.Generated, StringComparison.Ordinal);

    private static bool IsServing(PersistedSecret secret, DateTimeOffset now) =>
        secret.RetiresWhen > now && !secret.IsExpired(now);

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left < right ? left : right;

    /// <inheritdoc />
    public TenantKeyRolloverPlan Plan(
        IReadOnlyList<TenantKeySpecification> specifications,
        IReadOnlyCollection<PersistedSecret> held,
        IReadOnlySettingCollection settings,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(specifications);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(settings);

        var plan = new TenantKeyRolloverPlan { Now = now };
        var lifetimes = TenantKeyLifetimes.Read(
            settings,
            Options.KeyRollover.TokenLifetimeSettings
        );

        // A successor generated now is published for the activation delay at least: the pass that generates it runs
        // up to one interval after the predecessor enters the window.
        var horizon = now + lifetimes.ActivationDelay + Options.KeyRollover.Interval;

        foreach (var specification in specifications)
        {
            var isUsed = TenantKeyUsage.IsUsed(
                Options.KeyProvisioning.AlgorithmSettings,
                settings,
                specification.Algorithm
            );

            if (isUsed)
            {
                PlanUsedSpecification(plan, specification, held, lifetimes, horizon);
            }
            else
            {
                PlanUnusedSpecification(plan, specification, held, lifetimes);
            }
        }

        // An expired key verifies nothing a relying party could still accept; the ones the server generated leave
        // the store, the ones an operator supplied stay for the operator to remove.
        foreach (var secret in held)
        {
            if (IsGenerated(secret) && secret.IsExpired(now))
            {
                plan.Removals.Add(secret);
            }
        }

        return plan;
    }

    /// <inheritdoc />
    public bool HasServingKey(
        TenantKeySpecification specification,
        IReadOnlyCollection<PersistedSecret> held,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(held);

        return held.Any(secret =>
            IsServing(secret, now) && TenantKeyPolicy.IsSatisfiedBy(specification, secret)
        );
    }

    private void PlanUsedSpecification(
        TenantKeyRolloverPlan plan,
        TenantKeySpecification specification,
        IReadOnlyCollection<PersistedSecret> held,
        TenantKeyLifetimes lifetimes,
        DateTimeOffset horizon
    )
    {
        var now = plan.Now;
        var serving = held.Where(secret =>
                IsServing(secret, now) && TenantKeyPolicy.IsSatisfiedBy(specification, secret)
            )
            .ToList();

        // The instant after which no held key signs or encrypts for the algorithm.
        DateTimeOffset? coverageEnd =
            serving.Count == 0 ? null : serving.Max(secret => secret.RetiresWhen);

        if (!serving.Any(secret => secret.IsActive(now)))
        {
            var upcoming = serving.MinBy(secret => secret.ActivatesWhen);
            if (upcoming is not null)
            {
                // The key is published already, so activating it is the least disruptive way to sign again.
                plan.Activations.Add(
                    new TenantKeyActivation { Secret = upcoming, ActivatesWhen = now }
                );
            }
            else
            {
                // Nothing to hand over from, so the key activates at once; the delay would only prolong the outage.
                var retiresWhen = now + lifetimes.Lifetime;
                plan.Generations.Add(
                    new TenantKeyGeneration
                    {
                        Specification = specification,
                        ActivatesWhen = now,
                        RetiresWhen = retiresWhen,
                        ExpiresWhen = retiresWhen + lifetimes.RetirementGrace,
                        Reason = TenantKeyChangeReasons.NoActiveKey,
                    }
                );
                return;
            }
        }

        if (coverageEnd is not { } handOver || handOver > horizon)
        {
            return;
        }

        // The successor is published now and takes over the instant its predecessor retires, so one key of the
        // algorithm signs at any instant and a verifier has held the successor for the whole delay by then.
        var successorRetiresWhen = handOver + lifetimes.Lifetime;
        plan.Generations.Add(
            new TenantKeyGeneration
            {
                Specification = specification,
                ActivatesWhen = handOver,
                RetiresWhen = successorRetiresWhen,
                ExpiresWhen = successorRetiresWhen + lifetimes.RetirementGrace,
                Reason = TenantKeyChangeReasons.Successor,
            }
        );
    }

    // A key the server generated for the algorithm signs nothing the tenant's settings permit, so it retires now and
    // stays published for the grace, which covers every token it signed. A key an operator supplied is the
    // operator's to retire.
    private static void PlanUnusedSpecification(
        TenantKeyRolloverPlan plan,
        TenantKeySpecification specification,
        IReadOnlyCollection<PersistedSecret> held,
        TenantKeyLifetimes lifetimes
    )
    {
        var now = plan.Now;
        foreach (var secret in held)
        {
            if (
                !IsGenerated(secret)
                || !string.Equals(
                    secret.Algorithm,
                    specification.Algorithm,
                    StringComparison.Ordinal
                )
                || !IsServing(secret, now)
            )
            {
                continue;
            }

            plan.Retirements.Add(
                new TenantKeyRetirement
                {
                    Secret = secret,
                    RetiresWhen = now,
                    ExpiresWhen = Min(secret.ExpiresWhen, now + lifetimes.RetirementGrace),
                }
            );
        }
    }
}
