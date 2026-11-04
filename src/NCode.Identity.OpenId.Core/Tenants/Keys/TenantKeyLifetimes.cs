// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// The durations that shape the lifecycle of a key the server provisions for a tenant, read from the tenant's
/// effective settings: how long the key signs or encrypts, how long it is published before it does, and how long it
/// stays published after it stops. The grace is never shorter than the longest lifetime of the tokens the tenant
/// protects with its keys plus its clock skew, so no token outlives the key that verifies it.
/// </summary>
// See ADR-1404.
internal readonly record struct TenantKeyLifetimes
{
    /// <summary>
    /// Gets how long a key signs or encrypts, from its activation to its retirement.
    /// </summary>
    public required TimeSpan Lifetime { get; init; }

    /// <summary>
    /// Gets how long a key is published before it activates.
    /// </summary>
    public required TimeSpan ActivationDelay { get; init; }

    /// <summary>
    /// Gets how long a key stays published, and verifies or decrypts, after it retires.
    /// </summary>
    public required TimeSpan RetirementGrace { get; init; }

    /// <summary>
    /// Reads the lifetimes from a tenant's effective settings.
    /// </summary>
    /// <param name="settings">The tenant's effective settings.</param>
    /// <param name="tokenLifetimeSettings">The names of the settings that hold the lifetimes of the tokens the tenant
    /// protects with its keys, each of which the retirement grace covers.</param>
    /// <returns>The lifetimes.</returns>
    public static TenantKeyLifetimes Read(
        IReadOnlySettingCollection settings,
        IEnumerable<string> tokenLifetimeSettings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tokenLifetimeSettings);

        var longestTokenLifetime = TimeSpan.Zero;
        foreach (var settingName in tokenLifetimeSettings)
        {
            if (
                settings.TryGetValue(settingName, out var value)
                && value is TimeSpan lifetime
                && lifetime > longestTokenLifetime
            )
            {
                longestTokenLifetime = lifetime;
            }
        }

        var clockSkew = settings.GetValue(OpenIdSettingKeys.ClockSkew);
        var configuredGrace = settings.GetValue(OpenIdSettingKeys.TenantKeyRetirementGrace);
        var floor = longestTokenLifetime + clockSkew;

        return new TenantKeyLifetimes
        {
            Lifetime = settings.GetValue(OpenIdSettingKeys.TenantKeyLifetime),
            ActivationDelay = settings.GetValue(OpenIdSettingKeys.TenantKeyActivationDelay),
            RetirementGrace = configuredGrace > floor ? configuredGrace : floor,
        };
    }
}
