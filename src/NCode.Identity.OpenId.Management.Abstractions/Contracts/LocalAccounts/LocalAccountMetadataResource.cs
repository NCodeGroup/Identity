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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.LocalAccounts;

/// <summary>
/// Represents the REST resource for a local account's server-owned metadata bags, which live on the account's owning
/// principal and project into issued tokens and the UserInfo response (ADR-0054, ADR-0055). The
/// <see cref="ConcurrencyToken"/> is the owning principal's token.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountMetadataResource : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the identifier of the tenant the account belongs to.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the local account (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the account's owning principal (the actor the metadata belongs to).
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the owner-updatable profile metadata as a free-form JSON object.
    /// </summary>
    public required JsonElement ProfileMetadata { get; init; }

    /// <summary>
    /// Gets the server-controlled system metadata as a free-form JSON object. It is never end-user writable and may
    /// carry authorization-relevant data (ADR-0054, ADR-0055).
    /// </summary>
    public required JsonElement SystemMetadata { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }
}
