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

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Names the meter through which the Valkey shared-state adapter records its instruments, so a host can subscribe to
/// it, for example with OpenTelemetry's <c>AddMeter</c>.
/// </summary>
// See ADR-0197.
public static class ValkeyMetrics
{
    /// <summary>
    /// The name of the meter that records <c>ncode.identity.shared_state.operation.duration</c> for the Valkey adapter.
    /// </summary>
    public const string MeterName = "NCode.Identity.SharedState.Valkey";
}
