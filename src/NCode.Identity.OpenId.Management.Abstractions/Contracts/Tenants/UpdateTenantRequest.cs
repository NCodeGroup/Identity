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

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the mutable metadata of an OpenID Tenant that a JSON Patch document may modify. Settings and
/// secrets are managed through their own endpoints.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateTenantRequest
{
    /// <summary>
    /// Gets or sets the optional issuer identifier for the tenant — its request-independent identity, and therefore
    /// the origin of every absolute URL the server publishes for it. When <c>null</c>, that origin is derived from the
    /// incoming request, which is a development-only convenience.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is disabled.
    /// </summary>
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }
}
