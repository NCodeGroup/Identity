// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client.Metrics;

/// <summary>
/// Names the meter through which <c>NCode.Identity.OpenId.Client</c> records its instruments, so a host can subscribe
/// to it, for example with OpenTelemetry's <c>AddMeter</c>.
/// </summary>
// See ADR-1197.
[PublicAPI]
public static class OpenIdClientMetrics
{
    /// <summary>
    /// The name of the meter that the OpenID protocol client records its instruments on.
    /// </summary>
    public const string MeterName = "NCode.Identity.OpenId.Client";
}
