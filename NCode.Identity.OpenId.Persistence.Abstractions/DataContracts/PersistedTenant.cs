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

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> tenant.
/// </summary>
[PublicAPI]
public class PersistedTenant : ISupportId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// A value of <c>0</c> indicates that this entity has not been persisted to storage yet.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets or sets the natural identifier for this entity.
    /// </summary>
    [MaxLength(MaxLengths.TenantId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the domain name for this entity.
    /// This value is optional and can be used to find tenants by domain name.
    /// </summary>
    [MaxLength(MaxLengths.TenantDomainName)]
    public required string? DomainName { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the tenant settings.
    /// </summary>
    public required ConcurrentState<JsonElement> SettingsState { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to this tenant.
    /// </summary>
    public required ConcurrentState<IReadOnlyCollection<PersistedSecret>> SecretsState { get; init; }
}
