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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the options used to configure how the ambient tenant is resolved from an HTTP request. This is the
/// shared, low-level tenant-selection configuration consumed by both the OpenID runtime and the management API.
/// </summary>
[PublicAPI]
public sealed class TenantResolutionOptions
{
    /// <summary>
    /// Contains the default value for the root (control-plane) tenant's identifier.
    /// </summary>
    public const string DefaultRootTenantId = "root";

    /// <summary>
    /// Gets or sets the strategy code that selects the tenant-resolution strategy.
    /// This value is used to find the corresponding <see cref="ITenantStrategy"/>.
    /// The default value is <see cref="OpenIdConstants.TenantStrategyCodes.StaticSingle"/>.
    /// </summary>
    public string StrategyCode { get; set; } = OpenIdConstants.TenantStrategyCodes.StaticSingle;

    /// <summary>
    /// Gets or sets the identifier of the root (control-plane) tenant, into which the control-plane management
    /// resource server is seeded.
    /// The default value is <see cref="DefaultRootTenantId"/>.
    /// </summary>
    public string RootTenantId { get; set; } = DefaultRootTenantId;

    /// <summary>
    /// Gets or sets the options used when <see cref="StrategyCode"/> is
    /// <see cref="OpenIdConstants.TenantStrategyCodes.StaticSingle"/>.
    /// </summary>
    public StaticSingleTenantOptions? StaticSingle { get; set; }

    /// <summary>
    /// Gets or sets the options used when <see cref="StrategyCode"/> is
    /// <see cref="OpenIdConstants.TenantStrategyCodes.DynamicByHost"/>.
    /// </summary>
    public DynamicByHostTenantOptions? DynamicByHost { get; set; }

    /// <summary>
    /// Gets or sets the options used when <see cref="StrategyCode"/> is
    /// <see cref="OpenIdConstants.TenantStrategyCodes.DynamicByPath"/>.
    /// </summary>
    public DynamicByPathTenantOptions? DynamicByPath { get; set; }
}
