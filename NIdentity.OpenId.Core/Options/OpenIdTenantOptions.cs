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

namespace NIdentity.OpenId.Options;

public class OpenIdTenantOptions
{
    /// <summary>
    /// Gets or sets a value that indicates how multi-tenancy is configured.
    /// </summary>
    public TenantMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the relative path for the tenant.
    /// When the path is used to determine the tenant, this value must be set to a route pattern that
    /// includes the identifier as a route parameter (ex. '/{tenantId}') with a leading slash.
    /// </summary>
    public PathString TenantPath { get; set; }

    /// <summary>
    /// Gets or set the tenant options that are used when <see cref="Mode"/> is set to <see cref="TenantMode.StaticSingle"/>.
    /// </summary>
    public StaticSingleOpenIdTenantOptions? StaticSingle { get; set; }

    /// <summary>
    /// Gets or set the tenant options that are used when <see cref="Mode"/> is set to <see cref="TenantMode.DynamicByHost"/>.
    /// </summary>
    public DynamicByHostOpenIdTenantOptions? DynamicByHost { get; set; }

    /// <summary>
    /// Gets or set the tenant options that are used when <see cref="Mode"/> is set to <see cref="TenantMode.DynamicByPath"/>.
    /// </summary>
    public DynamicByPathOpenIdTenantOptions? DynamicByPath { get; set; }
}
