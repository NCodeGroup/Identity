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

    /// <inheritdoc cref="PersistedTenant.DomainName"/>
    public required string? DomainName { get; init; }

    /// <inheritdoc cref="PersistedTenant.IsDisabled"/>
    public required bool IsDisabled { get; init; }

    /// <inheritdoc cref="PersistedTenant.DisplayName"/>
    public required string DisplayName { get; init; }
}
