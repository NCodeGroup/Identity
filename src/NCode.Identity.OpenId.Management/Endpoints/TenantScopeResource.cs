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

using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// A minimal <see cref="ISupportTenantId"/> resource used to authorize a tenant-scoped collection operation (for
/// example, listing a tenant-bound family) against the ambient tenant, so that <c>TenantAdminHandler</c> applies
/// even though there is no single resource instance to evaluate.
/// </summary>
internal sealed class TenantScopeResource(string tenantId) : ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public string TenantId { get; } = tenantId;
}
