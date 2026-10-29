#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Diagnostics;

namespace NCode.Identity.Bff.Diagnostics;

/// <summary>
/// Owns the BFF's <see cref="ActivitySource"/> and the names of its spans. A span carries only the bounded attributes
/// of the matching instrument, never a token, handle, cookie, subject, client id or path.
/// </summary>
// See ADR-0298.
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
