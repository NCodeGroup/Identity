#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the resolution options for dynamic tenant resolution based on the host name.
/// </summary>
[PublicAPI]
public sealed class DynamicByHostTenantOptions
{
    /// <summary>
    /// Gets or sets the regex pattern to extract the domain name.
    /// The default pattern uses the entire value.
    /// </summary>
    public string RegexPattern { get; set; } = ".*";

    /// <summary>
    /// Gets or sets the relative base path for the tenant.
    /// When specified, this value must include a leading slash.
    /// The default value is undefined (empty).
    /// </summary>
    public PathString TenantPath { get; set; }

    /// <summary>
    /// Gets or sets the template a tenant's issuer is derived from when the tenant is written without one — an absolute
    /// URL that carries <see cref="TenantResolutionOptions.TenantIdPlaceholder"/> in its host, for example
    /// <c>https://{tenantId}.auth.example</c>. The derived issuer is stored with the tenant, so changing the template
    /// never moves an existing tenant. When unset, every tenant must be given an issuer explicitly.
    /// </summary>
    public string? IssuerTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template a tenant's mutual-TLS endpoint-alias authority (RFC 8705 §5) is derived from when the
    /// tenant is written without one — an https origin that carries <see cref="TenantResolutionOptions.TenantIdPlaceholder"/>
    /// in its host, for example <c>https://{tenantId}.mtls.example</c>. The derived alias is stored with the tenant, so
    /// changing the template never moves an existing tenant. When unset, a tenant has an alias only when it is given one.
    /// </summary>
    public string? MtlsEndpointAliasTemplate { get; set; }
}
