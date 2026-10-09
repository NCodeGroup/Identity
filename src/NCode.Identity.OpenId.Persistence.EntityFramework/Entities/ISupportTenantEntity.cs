#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Indicates that an entity is owned by a tenant and participates in the tenant-scoping global query filter.
/// </summary>
/// <remarks>
/// The owning tenant's normalized identifier is denormalized onto the entity (<see cref="NormalizedTenantId"/>) so the
/// framework's tenant-scope filter is a direct indexed column comparison rather than a navigation join, and so the
/// framework stays decoupled from any concrete tenant entity type (which lives in a slice package). The strongly-typed
/// tenant navigation, when present, is declared on the concrete slice entity, not on this framework interface.
/// </remarks>
[PublicAPI]
public interface ISupportTenantEntity
{
    /// <summary>
    /// Gets the owning tenant's normalized identifier, denormalized onto this entity so the tenant-scoping global
    /// query filter compares a single indexed column without a navigation join.
    /// </summary>
    string NormalizedTenantId { get; }
}
