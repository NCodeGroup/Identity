// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Metrics;

/// <summary>
/// Names the meter through which <c>NCode.Identity.OpenId.Authentication</c> records its instruments, so a host can
/// subscribe to it, for example with OpenTelemetry's <c>AddMeter</c>.
/// </summary>
// See ADR-1197.
public static class OpenIdAuthenticationMetrics
{
    /// <summary>
    /// The name of the meter that records the protocol-endpoint mechanism instruments, such as
    /// <c>ncode.identity.rate_limiting.requests</c>.
    /// </summary>
    public const string MeterName = "NCode.Identity.OpenId.Authentication";
}
