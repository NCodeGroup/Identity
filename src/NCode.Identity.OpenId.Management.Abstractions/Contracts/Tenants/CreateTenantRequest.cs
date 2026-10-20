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

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the request body to create a new OpenID Tenant.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateTenantRequest
{
    /// <summary>
    /// Gets the identifier for the tenant: a name its creator chooses, which cannot be changed once the tenant exists.
    /// It is 1 to 63 lowercase letters, digits, or hyphens that starts and ends with a letter or digit, so it is equally
    /// valid as a URL path segment and as a hostname label. It addresses the tenant in the management API and, when
    /// tenants are served by path, is the final segment of the tenant's issuer.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the optional issuer identifier for the tenant — its request-independent identity, and therefore the origin
    /// of every absolute URL the server publishes for it. It must be an absolute <c>https</c> URL (<c>http</c> only for
    /// a loopback host) with no user information, query, or fragment, that the configured tenant strategy serves the
    /// tenant beneath. When <c>null</c>, that origin is derived from the incoming request, which is a development-only
    /// convenience.
    /// </summary>
    public string? Issuer { get; init; }

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
