// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// The changes that bring a tenant's keys in line with its settings at one instant: the keys to generate, the
/// published keys to activate at once, the keys to retire, the keys whose expiry moves to the end of a raised grace,
/// and the expired keys to remove. The seed applies it when the tenant is materialized and the rollover pass applies
/// it in the background, so a tenant's keys converge on either path.
/// </summary>
// See ADR-1404.
internal sealed class TenantKeyRolloverPlan
{
    /// <summary>
    /// Gets the instant the plan was made at, which every change it lists is dated by.
    /// </summary>
    public required DateTimeOffset Now { get; init; }

    /// <summary>
    /// Gets the keys to generate.
    /// </summary>
    public List<TenantKeyGeneration> Generations { get; } = [];

    /// <summary>
    /// Gets the published keys to activate at once.
    /// </summary>
    public List<TenantKeyActivation> Activations { get; } = [];

    /// <summary>
    /// Gets the keys to retire.
    /// </summary>
    public List<TenantKeyRetirement> Retirements { get; } = [];

    /// <summary>
    /// Gets the keys whose expiry moves later, to the end of the retirement grace as it stands.
    /// </summary>
    public List<TenantKeyExtension> Extensions { get; } = [];

    /// <summary>
    /// Gets the expired keys to remove.
    /// </summary>
    public List<PersistedSecret> Removals { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the plan changes nothing.
    /// </summary>
    public bool IsEmpty =>
        Generations.Count == 0
        && Activations.Count == 0
        && Retirements.Count == 0
        && Extensions.Count == 0
        && Removals.Count == 0;
}
