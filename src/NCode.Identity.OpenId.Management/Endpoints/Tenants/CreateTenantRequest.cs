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
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Represents the request body to create a new OpenID Tenant.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateTenantRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the tenant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the optional domain name used to locate the tenant, or <c>null</c> when not applicable.
    /// </summary>
    public string? DomainName { get; init; }

    /// <summary>
    /// Gets the display name for the tenant.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tenant is created in a disabled state.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the initial JSON settings for the tenant.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
