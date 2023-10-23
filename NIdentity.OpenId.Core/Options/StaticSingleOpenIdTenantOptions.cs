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

namespace NIdentity.OpenId.Options;

public class StaticSingleOpenIdTenantOptions : CommonOpenIdTenantOptions
{
    /// <summary>
    /// Contains the default value for the <see cref="TenantId"/> property.
    /// </summary>
    public const string DefaultTenantId = "default";

    /// <summary>
    /// Contains the default value for the <see cref="DisplayName"/> property.
    /// </summary>
    public const string DefaultDisplayName = "Default Tenant";

    /// <summary>
    /// Gets or sets the identifier for the single-tenant.
    /// The default value is 'default'.
    /// </summary>
    public string? TenantId { get; set; } = DefaultTenantId;

    /// <summary>
    /// Gets or sets the display name for the single-tenant.
    /// The default value is 'Default Tenant'.
    /// </summary>
    public string? DisplayName { get; set; } = DefaultDisplayName;
}
