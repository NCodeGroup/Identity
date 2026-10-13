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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Represents the server-wide management capabilities discovery document, returned by
/// <c>GET /api/capabilities</c>. Mirroring the OpenID discovery document, it is <b>composed</b> from registered
/// capability providers rather than a fixed schema: each provider contributes a named section that is serialized
/// as a top-level member (via <see cref="JsonExtensionDataAttribute"/>), so new capabilities can be exposed by
/// registering a provider without changing this type.
/// </summary>
// See ADR-0067.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ManagementCapabilitiesResource
{
    /// <summary>
    /// Gets the composed capability sections, keyed by section name (for example <c>secretTypes</c>). Each entry
    /// is serialized as a top-level member of the document.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Capabilities { get; init; } = new(StringComparer.Ordinal);
}
