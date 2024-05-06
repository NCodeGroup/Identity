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
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> client.
/// </summary>
public class PersistedClient : ISupportId, ISupportConcurrencyToken, ISupportTenantId
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    public required long Id { get; set; }

    /// <summary>
    /// Gets or sets the natural tenant identifier for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the natural identifier for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string ClientId { get; set; }

    /// <inheritdoc/>
    [MaxLength(DataConstants.MaxConcurrencyTokenLength)]
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
    /// Gets or sets the collection of secrets only known to the client.
    /// </summary>
    public required List<PersistedSecret> Secrets { get; set; }

    /// <summary>
    /// Gets or sets the collection of redirect addresses registered for this client.
    /// </summary>
    public required List<string> RedirectUrls { get; set; }
}
