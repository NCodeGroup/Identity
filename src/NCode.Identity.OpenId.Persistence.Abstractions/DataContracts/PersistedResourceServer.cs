#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted resource server (an API, in Auth0 terms). A resource server owns its scopes and
/// is identified by its audience <see cref="Identifier"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedResourceServer : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that owns this resource server.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the opaque, server-generated public identifier for this resource server.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets or sets the operator-supplied audience identifier for this resource server.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.ResourceServerIdentifier)]
    public required string Identifier { get; init; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name for this resource server.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.DisplayName)]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this resource server is a reserved, system-owned resource server that
    /// cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this resource server is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the JSON settings for this resource server.
    /// </summary>
    public required JsonElement Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of scopes owned by this resource server.
    /// </summary>
    public required IReadOnlyCollection<PersistedScope> Scopes { get; set; }
}
