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

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options that are used to configure how a resolved tenant is materialized (settings/secrets refresh
/// and caching). Tenant <em>selection</em> is configured separately via
/// <see cref="NCode.Identity.OpenId.Tenants.TenantResolutionOptions"/>.
/// </summary>
[PublicAPI]
public sealed class OpenIdTenantOptions
{
    /// <summary>
    /// Gets or sets the period of time after which the settings for a tenant are refreshed.
    /// The default value is 5 minutes.
    /// </summary>
    public TimeSpan SettingsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets the period of time after which the secrets for a tenant are refreshed.
    /// The default value is 15 minutes.
    /// </summary>
    public TimeSpan SecretsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(15.0);

    /// <summary>
    /// Gets or sets the period of time after which a tenant is removed from the cache.
    /// The cache uses a sliding expiration.
    /// The default value is 4 hours.
    /// </summary>
    public TimeSpan TenantCacheExpiration { get; set; } = TimeSpan.FromHours(4.0);
}
