// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Names the meter and the activity source through which the BFF records its instruments and spans, so a host can
/// subscribe to them, for example with OpenTelemetry's <c>AddMeter</c> and <c>AddSource</c>.
/// </summary>
// See ADR-1197.
[PublicAPI]
public static class BffTelemetry
{
    /// <summary>
    /// The name of the meter that records <c>ncode.identity.bff.token.refresh.duration</c> and
    /// <c>ncode.identity.bff.session_store.operation.duration</c>.
    /// </summary>
    public const string MeterName = "NCode.Identity.Bff";

    /// <summary>
    /// The name of the activity source that traces token refreshes and session store operations.
    /// </summary>
    public const string ActivitySourceName = "NCode.Identity.Bff";
}
