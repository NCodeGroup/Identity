// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyRolloverPlanner"/>. For every provisioned key the
/// tenant's settings use, exactly one held key signs or encrypts at a time: when none does, a published key activates
/// at once or a key is generated that activates at once; successors are generated, each published from now and
/// activating at its predecessor's retirement, until the held keys sign past the activation delay plus the pass
/// interval, so a verifier holds every successor for the whole delay before the first token it signs. A key the
/// rollover provisioned for an algorithm the tenant's settings do not use retires now and expires at the end of the
/// retirement grace; one whose retirement plus the grace, as it stands, reaches past its expiry expires at the end of
/// the grace instead; and one that has expired is removed. A key the rollover did not provision keeps the lifecycle
/// its creator gave it. When the tenant's settings choose another custody for a provisioned key, the key held by the
/// new custody is generated to activate after the activation delay, and the key the rollover provisioned in the old
/// custody retires then.
/// </summary>
// See ADR-1404.
internal sealed class DefaultTenantKeyRolloverPlanner(
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy
) : ITenantKeyRolloverPlanner
{
    /// <summary>
    /// Determines whether the rollover manages the key's lifecycle: only a key it provisioned is activated, retired,
    /// extended or removed.
    /// </summary>
    private static bool IsManaged(PersistedSecret secret) =>
        string.Equals(secret.ManagedBy, SecretManagers.TenantKeyRollover, StringComparison.Ordinal);

    /// <summary>
    /// Determines whether the key serves its algorithm at <paramref name="now"/>: it has neither retired nor expired,
    /// so it signs or encrypts, or is published ahead of doing so.
    /// </summary>
    private static bool IsServing(PersistedSecret secret, DateTimeOffset now) =>
        secret.RetiresWhen > now && !secret.IsExpired(now);

    /// <summary>
    /// Returns the earlier of two instants.
    /// </summary>
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left < right ? left : right;

    /// <summary>
    /// Returns the later of two instants.
    /// </summary>
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) =>
        left > right ? left : right;

    private static TenantKeyGeneration CreateGeneration(
        TenantKeySpecification specification,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        TenantKeyLifetimes lifetimes,
        string reason
    ) =>
        new()
        {
            Specification = specification,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = retiresWhen + lifetimes.RetirementGrace,
            Reason = reason,
        };

    // A key the rollover provisioned for the algorithm signs nothing the tenant's settings permit, so it retires now
    // and stays published for the grace, which covers every token it signed and every message encrypted to it. A key
    // published ahead of an activation that never comes activates and retires at the same instant, so its instants
    // stay in order. A key the rollover did not provision is its creator's to retire.
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
                !IsManaged(secret)
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
                    ActivatesWhen = Min(secret.ActivatesWhen, now),
                    RetiresWhen = now,
                    ExpiresWhen = now + lifetimes.RetirementGrace,
                    Reason = TenantKeyChangeReasons.UnusedAlgorithm,
                }
            );
        }
    }

    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;

    // Read on use, never in the constructor, so the options are not built while the container composes the planner.
    private OpenIdTenantOptions Options => OptionsAccessor.Value.Tenant;

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

        // The tenant's custody choice is part of every provisioned key, so the plan is made for the keys as the
        // tenant is provisioned with them.
        specifications = TenantKeyPolicy.ForTenant(specifications, settings);

        var options = Options;
        var plan = new TenantKeyRolloverPlan { Now = now };
        var lifetimes = TenantKeyLifetimes.Read(
            settings,
            options.KeyRollover.TokenLifetimeSettings
        );

        // A successor generated now is published for the activation delay at least: the pass that generates it runs
        // up to one interval after the predecessor enters the window.
        var horizon = now + lifetimes.ActivationDelay + options.KeyRollover.Interval;

        foreach (var specification in specifications)
        {
            var isUsed = TenantKeyUsage.IsUsed(
                options.KeyProvisioning.AlgorithmSettings,
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

        var retiring = plan.Retirements.Select(retirement => retirement.Secret).ToHashSet();
        foreach (var secret in held)
        {
            if (!IsManaged(secret))
            {
                continue;
            }

            // An expired key verifies nothing a relying party could still accept, so it leaves the store.
            if (secret.IsExpired(now))
            {
                plan.Removals.Add(secret);
                continue;
            }

            // The grace covers the tokens the key protected as the tenant's settings and token lifetimes stand, which
            // may have grown since the key was generated; an expiry only ever moves later. A key activated in this
            // plan is extended too: the rollover applies the extension to the key as the activation left it.
            var graceEnd = secret.RetiresWhen + lifetimes.RetirementGrace;
            if (graceEnd > secret.ExpiresWhen && !retiring.Contains(secret))
            {
                plan.Extensions.Add(
                    new TenantKeyExtension { Secret = secret, ExpiresWhen = graceEnd }
                );
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

        // The keys that serve the algorithm in the tenant's custody, and the rollover's own keys that serve it in
        // another custody: those the hand-over replaces.
        var serving = new List<PersistedSecret>();
        var superseded = new List<PersistedSecret>();
        foreach (var secret in held)
        {
            if (!IsServing(secret, now))
            {
                continue;
            }

            if (TenantKeyPolicy.IsSatisfiedBy(specification, secret))
            {
                serving.Add(secret);
            }
            else if (
                IsManaged(secret)
                && string.Equals(
                    secret.Algorithm,
                    specification.Algorithm,
                    StringComparison.Ordinal
                )
            )
            {
                superseded.Add(secret);
            }
        }

        var activeSuperseded = superseded.Where(secret => secret.IsActive(now)).ToList();

        // The instant after which no held key signs or encrypts for the algorithm, and the instant the superseded keys
        // hand over at.
        DateTimeOffset coverageEnd;
        DateTimeOffset handOver;
        if (serving.Count > 0)
        {
            coverageEnd = serving.Max(secret => secret.RetiresWhen);
            handOver = Max(now, serving.Min(secret => secret.ActivatesWhen));

            // The key is published already, so activating it is the least disruptive way to sign again, unless a
            // superseded key still signs until the hand-over.
            if (
                !serving.Any(secret => secret.IsActive(now))
                && activeSuperseded.Count == 0
                && serving.MinBy(secret => secret.ActivatesWhen) is { } upcoming
            )
            {
                plan.Activations.Add(
                    new TenantKeyActivation { Secret = upcoming, ActivatesWhen = now }
                );
            }
        }
        else if (activeSuperseded.Count > 0)
        {
            // The key in the tenant's custody is published for the whole delay before it signs, so a verifier that
            // cached the JWKS holds it by then; the superseded keys sign until it activates, or until the last of them
            // retires on its own when that comes first.
            handOver = Min(
                now + lifetimes.ActivationDelay,
                activeSuperseded.Max(secret => secret.RetiresWhen)
            );
            coverageEnd = handOver + lifetimes.Lifetime;
            plan.Generations.Add(
                CreateGeneration(
                    specification,
                    handOver,
                    coverageEnd,
                    lifetimes,
                    TenantKeyChangeReasons.CustodyChanged
                )
            );
        }
        else
        {
            // Nothing to hand over from, so the key activates at once; the delay would only prolong the outage.
            handOver = now;
            coverageEnd = now + lifetimes.Lifetime;
            plan.Generations.Add(
                CreateGeneration(
                    specification,
                    now,
                    coverageEnd,
                    lifetimes,
                    TenantKeyChangeReasons.NoActiveKey
                )
            );
        }

        // A superseded key that signs retires at the hand-over, unless it retires sooner on its own; one published
        // ahead of an activation retires at once, so its active window collapses to nothing and its instants stay in
        // order.
        foreach (var secret in superseded)
        {
            if (secret.IsActive(now))
            {
                if (secret.RetiresWhen > handOver)
                {
                    plan.Retirements.Add(
                        new TenantKeyRetirement
                        {
                            Secret = secret,
                            ActivatesWhen = secret.ActivatesWhen,
                            RetiresWhen = handOver,
                            ExpiresWhen = handOver + lifetimes.RetirementGrace,
                            Reason = TenantKeyChangeReasons.CustodyChanged,
                        }
                    );
                }
            }
            else
            {
                plan.Retirements.Add(
                    new TenantKeyRetirement
                    {
                        Secret = secret,
                        ActivatesWhen = Min(secret.ActivatesWhen, now),
                        RetiresWhen = now,
                        ExpiresWhen = now + lifetimes.RetirementGrace,
                        Reason = TenantKeyChangeReasons.CustodyChanged,
                    }
                );
            }
        }

        // Each successor is published now and takes over the instant its predecessor retires, so one key of the
        // algorithm signs at any instant and a verifier has held the successor for the whole delay by then. The chain
        // reaches past the horizon in one plan, so a lifetime shorter than the delay plus the interval leaves no gap:
        // the tenant then holds as many upcoming keys as fit in the delay plus the interval.
        while (coverageEnd <= horizon)
        {
            var successorRetiresWhen = coverageEnd + lifetimes.Lifetime;
            plan.Generations.Add(
                CreateGeneration(
                    specification,
                    coverageEnd,
                    successorRetiresWhen,
                    lifetimes,
                    TenantKeyChangeReasons.Successor
                )
            );
            coverageEnd = successorRetiresWhen;
        }
    }
}
