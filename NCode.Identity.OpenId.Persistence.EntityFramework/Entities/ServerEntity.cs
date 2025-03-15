#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> or <c>OpenID Connect</c> server.
/// The complimentary DTO for this entity is <see cref="PersistedServer"/>.
/// </summary>
[Index(nameof(NormalizedServerId), IsUnique = true)]
public class ServerEntity : ISupportId, ISupportConcurrencyToken
{
    /// <inheritdoc />
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets or sets the natural identifier for this entity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ServerId)]
    public required string ServerId { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="ServerId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ServerId)]
    public required string NormalizedServerId { get; init; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for server's settings.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SettingsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for the server's secrets.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SecretsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the server's settings.
    /// </summary>
    public required JsonElement SettingsJson { get; set; }

    // navigation properties

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets or sets the collection of secrets only known to this server.
    /// </summary>
    public required IEnumerable<ServerSecretEntity> Secrets { get; init; }
}
