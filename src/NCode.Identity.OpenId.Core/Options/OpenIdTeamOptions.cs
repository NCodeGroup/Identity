// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options that are used to configure how a team is materialized (settings refresh and caching) and how
/// often a replica reconciles a team's settings.
/// </summary>
// See ADR-1403
[PublicAPI]
public sealed class OpenIdTeamOptions
{
    /// <summary>
    /// Gets or sets the period of time after which the settings for a team are refreshed.
    /// The default value is 5 minutes.
    /// </summary>
    public TimeSpan SettingsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets the period of time after which a materialized team is removed from the cache, however often
    /// it is used, so a replica that missed the announcement of a change to the team serves it at most this long.
    /// The default value is 15 minutes.
    /// </summary>
    public TimeSpan TeamCacheLifetime { get; set; } = TimeSpan.FromMinutes(15.0);

    /// <summary>
    /// Gets or sets the period of time that one replica holds the right to reconcile a team's settings, so no other
    /// replica reconciles the same team while the lease lasts.
    /// The default value is 10 minutes.
    /// </summary>
    public TimeSpan ReconciliationLeaseDuration { get; set; } = TimeSpan.FromMinutes(10.0);
}
