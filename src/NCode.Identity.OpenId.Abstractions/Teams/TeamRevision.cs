// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Identifies the state of a team a replica serves: the team's identifier and the concurrency token of the persisted
/// team it was materialized from.
/// </summary>
/// <param name="TeamId">The identifier of the team.</param>
/// <param name="ConcurrencyToken">The concurrency token of the persisted team the cached team was materialized from.</param>
// See ADR-1416.
[PublicAPI]
public readonly record struct TeamRevision(string TeamId, string ConcurrencyToken);
