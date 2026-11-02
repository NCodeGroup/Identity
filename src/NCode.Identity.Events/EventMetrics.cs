// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events;

/// <summary>
/// Names the meter through which <c>NCode.Identity.Events</c> counts published events, so a host can subscribe to it,
/// for example with OpenTelemetry's <c>AddMeter</c>.
/// </summary>
// See ADR-1197.
public static class EventMetrics
{
    /// <summary>
    /// The name of the meter that records <c>ncode.identity.events.published</c>.
    /// </summary>
    public const string MeterName = "NCode.Identity.Events";
}
