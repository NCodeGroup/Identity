// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Identifies the state of a tenant a replica serves: the tenant's identifier and the concurrency token of the persisted
/// tenant it was materialized from.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
/// <param name="ConcurrencyToken">The concurrency token of the persisted tenant the cached tenant was materialized from.</param>
// See ADR-1454.
[PublicAPI]
public readonly record struct TenantRevision(string TenantId, string ConcurrencyToken);
