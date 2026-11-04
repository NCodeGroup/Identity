// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Contains constants for the values of <see cref="PersistedSecret.ManagedBy"/> this family writes: the components
/// that manage a stored secret's lifecycle on their own. An open vocabulary: a package that provisions and retires
/// secrets of its own names itself.
/// </summary>
// See ADR-1404.
[PublicAPI]
public static class SecretManagers
{
    /// <summary>
    /// The rollover of the keys the server provisions for a tenant: it generates a key, generates its successor ahead
    /// of its retirement, retires it when the tenant's settings do not use its algorithm, extends its expiry when
    /// the retirement grace is raised, and removes it once it has expired. A tenant key with any other value, or
    /// none, keeps the lifecycle its creator gave it.
    /// </summary>
    public const string TenantKeyRollover = "tenant_key_rollover";
}
