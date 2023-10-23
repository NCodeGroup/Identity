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

using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Options;

public class CommonOpenIdTenantOptions
{
    /// <summary>
    /// Gets or sets the relative path for the tenant.
    /// Hosts that use <see cref="TenantMode.DynamicByPath"/> mode should set this value to a route pattern that
    /// includes the tenant identifier as a route parameter (i.e. '/{tenantId}').
    /// </summary>
    public PathString TenantPath { get; set; }

    /// <summary>
    /// Gets or sets the issuer identifier for the tenant.
    /// If specified, the host address must be in unicode and not punycode.
    /// If unspecified, the default value will be determined by the host address and tenant path.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the configurable settings for the tenant.
    /// </summary>
    public TenantConfiguration Configuration { get; set; } = new();
}
