// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Represents a mediator command that seeds a tenant with its reserved, system-owned data (for example, the system
/// resource servers and any bootstrap data). Every registered handler runs, fanning out the seed across the
/// subsystems that contribute to a tenant. Handlers share the unit of work on <see cref="TenantSeedContext"/> and
/// must be idempotent, because a tenant may be seeded more than once (for example, when reconciling after an upgrade).
/// </summary>
/// <param name="Context">The shared <see cref="TenantSeedContext"/> for the tenant being seeded.</param>
[PublicAPI]
public readonly record struct SeedTenantCommand(TenantSeedContext Context) : ICommand;
