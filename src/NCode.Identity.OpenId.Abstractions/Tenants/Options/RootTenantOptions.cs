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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the options for the root (control-plane) tenant, which configuration defines and the server provisions on
/// demand under every tenant strategy.
/// </summary>
[PublicAPI]
public sealed class RootTenantOptions
{
    /// <summary>
    /// Contains the default value for the root tenant's identifier.
    /// </summary>
    public const string DefaultTenantId = "root";

    /// <summary>
    /// Contains the default value for the root tenant's display name.
    /// </summary>
    public const string DefaultDisplayName = "Root Tenant";

    /// <summary>
    /// Gets or sets the identifier of the root tenant, into which the control-plane management resource server is
    /// seeded. The default value is <see cref="DefaultTenantId"/>.
    /// </summary>
    public string TenantId { get; set; } = DefaultTenantId;

    /// <summary>
    /// Gets or sets the display name the root tenant is created with. The default value is
    /// <see cref="DefaultDisplayName"/>.
    /// </summary>
    public string DisplayName { get; set; } = DefaultDisplayName;

    /// <summary>
    /// Gets or sets the issuer the root tenant is created with, when it differs from the one the tenant strategy derives
    /// for it from its issuer template — for example <c>https://auth.example</c> beside workload tenants at
    /// <c>https://{tenantId}.auth.example</c>.
    /// </summary>
    /// <remarks>
    /// Applied when the root tenant is first provisioned; it does not update an existing tenant. A strategy that does not
    /// serve the root tenant (the static single-tenant strategy, unless its tenant is the root tenant) rejects it.
    /// </remarks>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the mutual-TLS endpoint-alias authority (RFC 8705 §5) the root tenant is created with, when it differs
    /// from the one the tenant strategy derives for it from its alias template — for example <c>https://mtls.example</c>
    /// beside workload tenants at <c>https://{tenantId}.mtls.example</c>. An https origin.
    /// </summary>
    /// <remarks>
    /// Applied when the root tenant is first provisioned; it does not update an existing tenant. A strategy that does not
    /// serve the root tenant (the static single-tenant strategy, unless its tenant is the root tenant) rejects it.
    /// </remarks>
    public string? MtlsEndpointAliasAuthority { get; set; }
}
