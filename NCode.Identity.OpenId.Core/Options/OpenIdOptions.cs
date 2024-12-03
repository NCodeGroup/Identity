#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Options;

/// <summary>
/// Contains the options used to configure OpenID.
/// </summary>
[PublicAPI]
public class OpenIdOptions
{
    /// <summary>
    /// Gets the default name of the configuration section.
    /// </summary>
    public const string DefaultSectionName = "OpenId";

    /// <summary>
    /// Gets or sets the name of the configuration section.
    /// </summary>
    public string SectionName { get; set; } = DefaultSectionName;

    /// <summary>
    /// Gets or sets the configurable options for server features.
    /// </summary>
    public OpenIdServerOptions Server { get; set; } = new();

    /// <summary>
    /// Gets or sets the configurable options for tenant features.
    /// </summary>
    public OpenIdTenantOptions Tenant { get; set; } = new();
}
