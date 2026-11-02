// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Names the meter through which the Valkey shared-state adapter records its instruments, so a host can subscribe to
/// it, for example with OpenTelemetry's <c>AddMeter</c>.
/// </summary>
// See ADR-1197.
public static class ValkeyMetrics
{
    /// <summary>
    /// The name of the meter that records <c>ncode.identity.shared_state.operation.duration</c> for the Valkey adapter.
    /// </summary>
    public const string MeterName = "NCode.Identity.SharedState.Valkey";
}
