// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace NCode.Identity.Bff.Diagnostics;

/// <summary>
/// Owns the BFF's <see cref="ActivitySource"/> and the names of its spans. A span carries only the bounded attributes
/// of the matching instrument, never a token, handle, cookie, subject, client id or path.
/// </summary>
// See ADR-1197.
internal static class BffActivities
{
    /// <summary>
    /// The name of the span around a user access-token refresh, waits for another replica's refresh included.
    /// </summary>
    public const string TokenRefresh = "ncode.identity.bff.token.refresh";

    /// <summary>
    /// The name of the span around one session store operation.
    /// </summary>
    public const string SessionStoreOperation = "ncode.identity.bff.session_store.operation";

    /// <summary>
    /// Gets the activity source named <see cref="BffTelemetry.ActivitySourceName"/>.
    /// </summary>
    public static ActivitySource Source { get; } = new(BffTelemetry.ActivitySourceName);
}
