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

namespace NCode.Identity.OpenId.Options;

public class StaticSingleOpenIdTenantOptions
{
    /// <summary>
    /// Contains the default value for the tenant's identifier.
    /// </summary>
    public const string DefaultTenantId = "default";

    /// <summary>
    /// Contains the default value for the tenant's display name.
    /// </summary>
    public const string DefaultDisplayName = "Default Tenant";

    /// <summary>
    /// Gets or sets the identifier for the tenant.
    /// </summary>
    public string TenantId { get; set; } = DefaultTenantId;

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    public string DisplayName { get; set; } = DefaultDisplayName;

    /// <summary>
    /// Gets or sets the relative base path for the tenant.
    /// When specified, this value must include a leading slash.
    /// The default value is undefined (aka empty).
    /// </summary>
    public PathString TenantPath { get; set; }
}
