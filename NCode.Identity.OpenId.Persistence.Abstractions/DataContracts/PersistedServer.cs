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

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> server.
/// </summary>
[PublicAPI]
public class PersistedServer : ISupportId
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// A value of <c>0</c> indicates that this entity has not been persisted to storage yet.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the natural identifier for this entity.
    /// </summary>
    [MaxLength(MaxLengths.ServerId)]
    public required string ServerId { get; init; }

    /// <summary>
    /// Gets or sets the serialized JSON for the tenant settings.
    /// </summary>
    public required ConcurrentState<JsonElement> SettingsState { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to this tenant.
    /// </summary>
    public required ConcurrentState<IReadOnlyCollection<PersistedSecret>> SecretsState { get; set; }
}
