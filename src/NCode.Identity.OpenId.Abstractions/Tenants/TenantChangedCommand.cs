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
/// Represents a mediator command that announces a committed change to a tenant: its record, its settings, or its secrets
/// changed, or it was deleted. Every registered handler runs. The default handler evicts the tenant from this
/// replica's <see cref="IOpenIdTenantCache"/>, so the change takes effect at once, announces the change on the
/// replica broadcast channel so that every other replica evicts the tenant too, and re-seeds the tenant when its
/// settings changed, so a key its new settings require is provisioned without waiting for the tenant to be
/// materialized again. Send it after the change commits; handlers must be idempotent, because one change may be
/// announced more than once.
/// </summary>
/// <param name="TenantId">The identifier of the tenant that changed.</param>
/// <param name="SettingsChanged"><see langword="true"/> when the tenant's settings changed; otherwise,
/// <see langword="false"/>. Only a settings change re-seeds the tenant: a deleted secret is not provisioned again in
/// the request that deleted it, and a deleted tenant is not seeded at all.</param>
// See ADR-0169 (every tenant is provisioned with persisted keys) and ADR-0205 (replicas announce changes through a
// broadcast port).
[PublicAPI]
public readonly record struct TenantChangedCommand(string TenantId, bool SettingsChanged)
    : ICommand;
