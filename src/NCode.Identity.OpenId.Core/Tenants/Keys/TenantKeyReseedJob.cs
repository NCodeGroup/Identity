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

using System.Text.Json;
using NCode.Identity.Dispatch;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// A self-contained, serializable description of a re-seed of every tenant whose effective algorithm lists newly name a
/// configured algorithm after a change to the server's settings. It is handed to the
/// <see cref="NCode.Identity.Dispatch.IJobDispatcher"/> so the pass over every tenant runs out of band rather than in the
/// request that changed the settings, and can be routed through a durable or distributed dispatcher adapter.
/// </summary>
// See ADR-0169 (decision 10).
internal sealed record TenantKeyReseedJob : IDispatchJob
{
    /// <summary>
    /// Gets the server's persisted settings document before the change.
    /// </summary>
    public required JsonElement PreviousServerSettings { get; init; }

    /// <summary>
    /// Gets the server's persisted settings document after the change.
    /// </summary>
    public required JsonElement ServerSettings { get; init; }

    /// <summary>
    /// Gets the configured algorithms that some algorithm list of the server's effective settings gained, and so the
    /// algorithms a tenant may now need a key for.
    /// </summary>
    public required IReadOnlyList<string> Algorithms { get; init; }
}
