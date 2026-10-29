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
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedTenant"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedTenant.Issuer"/>
    public required string? Issuer { get; init; }

    /// <inheritdoc cref="PersistedTenant.MtlsEndpointAliasAuthority"/>
    public required string? MtlsEndpointAliasAuthority { get; init; }

    /// <inheritdoc cref="PersistedTenant.IsDisabled"/>
    public required bool IsDisabled { get; init; }

    /// <inheritdoc cref="PersistedTenant.DisplayName"/>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the tenant's effective environment: the <c>environment_type</c> and <c>environment_name</c> its settings
    /// declare, or the server's when it declares none. <see langword="null"/> when the environment cannot be determined
    /// — the tenant's or the server's stored settings cannot be honored (ADR-0245), or they name a type that is not
    /// registered — so that a tenant whose settings need repair is still listed. Read-only; a tenant declares its
    /// environment through its settings. An audit record's resource values carry only the tenant's own state, so they
    /// omit it.
    /// </summary>
    // See ADR-0289.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TenantEnvironmentResource? Environment { get; init; }
}
