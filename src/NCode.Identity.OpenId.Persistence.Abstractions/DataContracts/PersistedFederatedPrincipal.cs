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
using NCode.Json;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted federated principal: a tenant-scoped representation of a human actor that owns
/// many external connection identities (<see cref="PersistedFederatedIdentity"/>). The principal's
/// <see cref="PrincipalId"/> is the stable, server-generated identifier that authority references and that is emitted
/// as the <c>sub</c> claim.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedFederatedPrincipal : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that owns this principal.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the server-generated opaque public identifier of the principal.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets or sets the principal's owner-updatable profile metadata as a free-form JSON object, defaulting to an empty
    /// object <c>{}</c>. This bag is intended to be read, and updated, by the account owner and must never drive
    /// authorization (ADR-0055).
    /// </summary>
    public JsonElement ProfileMetadata { get; set; } = JsonElements.EmptyObject;

    /// <summary>
    /// Gets or sets the principal's server-controlled system metadata as a free-form JSON object, defaulting to an
    /// empty object <c>{}</c>. This bag is managed only by the server or an administrator and must never be writable by
    /// the end user, as it may carry authorization-relevant data (ADR-0055).
    /// </summary>
    public JsonElement SystemMetadata { get; set; } = JsonElements.EmptyObject;

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }
}
