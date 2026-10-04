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
