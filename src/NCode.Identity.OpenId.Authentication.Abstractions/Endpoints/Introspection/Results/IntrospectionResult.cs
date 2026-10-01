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

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;

/// <summary>
/// Contains the parameters for an <c>OAuth 2.0</c> token introspection response
/// (<see href="https://datatracker.ietf.org/doc/html/rfc7662">RFC 7662</see>). When the token is not active, only
/// <see cref="Active"/> is emitted; otherwise the response is enriched with the token's claims.
/// </summary>
[PublicAPI]
public sealed class IntrospectionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the token is currently active.
    /// </summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets the additional claims describing an active token (for example, <c>scope</c>, <c>client_id</c>,
    /// <c>sub</c>, <c>aud</c>, and <c>exp</c>). Contributing handlers add to this collection only when the token is
    /// active.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Claims { get; set; } = new();
}
