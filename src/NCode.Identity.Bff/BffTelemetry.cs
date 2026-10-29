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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Names the meter and the activity source through which the BFF records its instruments and spans, so a host can
/// subscribe to them, for example with OpenTelemetry's <c>AddMeter</c> and <c>AddSource</c>.
/// </summary>
// See ADR-0197 and ADR-0298.
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
