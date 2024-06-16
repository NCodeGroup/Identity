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
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> client.
/// </summary>
[PublicAPI]
public class PersistedClient : ISupportId, ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// A value of <c>0</c> indicates that this entity has not been persisted to storage yet.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets or sets the natural tenant identifier for this entity.
    /// </summary>
    [MaxLength(MaxLengths.TenantId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the natural identifier for this entity.
    /// </summary>
    [MaxLength(MaxLengths.ClientId)]
    public required string ClientId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the client is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the client settings.
    /// </summary>
    public required JsonElement Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to this client.
    /// </summary>
    public required IReadOnlyCollection<PersistedSecret> Secrets { get; init; }

    /// <summary>
    /// Gets or sets the collection of redirect addresses registered for this client.
    /// </summary>
    public required IReadOnlyCollection<string> RedirectUrls { get; init; }
}
