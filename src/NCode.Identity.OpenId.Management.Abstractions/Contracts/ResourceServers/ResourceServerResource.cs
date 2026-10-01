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
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the REST resource for a resource server (an API, in Auth0 terms).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ResourceServerResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the opaque, server-generated public identifier for this resource server.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the operator-supplied audience identifier for this resource server.
    /// </summary>
    public required string Identifier { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets the human-readable name for this resource server.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether this resource server is a reserved, system-owned resource server that cannot
    /// be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }

    /// <summary>
    /// Gets a value indicating whether this resource server is disabled.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the JSON settings for this resource server.
    /// </summary>
    public required JsonElement Settings { get; init; }

    /// <summary>
    /// Gets the scopes owned by this resource server.
    /// </summary>
    public required IReadOnlyList<ScopeResource> Scopes { get; init; }
}
