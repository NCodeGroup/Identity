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
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// A minimal <see cref="ISupportTenantId"/> authorization resource used to authorize a tenant-scoped operation (for
/// example, creating or listing a tenant-bound family) against the ambient tenant. Unlike <see cref="ResourceNode"/> it
/// deliberately carries no resource type or identifier, so only the tenant- and global-admin handlers evaluate it and
/// the ownership handler does not — there is no single resource instance (and no ownership) to consider.
/// </summary>
[PublicAPI]
public sealed class TenantScopeResource : ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Creates a <see cref="TenantScopeResource"/> for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to authorize against.</param>
    /// <returns>The tenant-scope resource.</returns>
    public static TenantScopeResource For(string tenantId) => new() { TenantId = tenantId };
}
