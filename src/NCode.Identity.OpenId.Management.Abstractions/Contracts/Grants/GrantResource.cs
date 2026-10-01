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
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Contracts.Grants;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedGrant"/> instance. This is a metadata-only projection:
/// the internal <c>HashedKey</c> and the grant <c>PayloadJson</c> are intentionally never exposed.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GrantResource
{
    /// <summary>
    /// Gets the opaque, server-generated public identifier for this grant.
    /// </summary>
    public required string GrantId { get; init; }

    /// <summary>
    /// Gets the type of grant.
    /// </summary>
    public required string GrantType { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that owns this grant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the client associated with this grant, or <c>null</c> when not applicable.
    /// </summary>
    public required string? ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the subject associated with this grant, or <c>null</c> when not applicable.
    /// </summary>
    public required string? SubjectId { get; init; }

    /// <summary>
    /// Gets when this grant was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets when this grant expires, or <c>null</c> when it does not expire.
    /// </summary>
    public required DateTimeOffset? ExpiresWhen { get; init; }

    /// <summary>
    /// Gets when this grant was revoked, or <c>null</c> when it has not been revoked.
    /// </summary>
    public required DateTimeOffset? RevokedWhen { get; init; }

    /// <summary>
    /// Gets when this grant was consumed, or <c>null</c> when it has not been consumed.
    /// </summary>
    public required DateTimeOffset? ConsumedWhen { get; init; }
}
