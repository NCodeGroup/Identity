// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Dispatch;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// A self-contained, serializable request to sweep the keys that one remote key provider holds for this deployment and
/// that no stored secret names. The sweep service dispatches one job per provider that creates keys on its schedule,
/// so each provider has its own retries.
/// </summary>
// See ADR-1478.
internal sealed record RemoteKeySweepJob : IDispatchJob
{
    /// <summary>
    /// Gets the name of the remote key provider whose orphaned keys are swept.
    /// </summary>
    public required string Provider { get; init; }
}
