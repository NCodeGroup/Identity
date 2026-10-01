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

using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the default implementation of <see cref="IResourceOwnershipService"/>.
/// </summary>
internal class DefaultResourceOwnershipService(ICryptoService cryptoService)
    : IResourceOwnershipService
{
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public async ValueTask AssignCreatorAsync(
        ClaimsPrincipal user,
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var principalId = user.FindFirstValue(JoseClaimNames.Payload.Sub);
        if (string.IsNullOrEmpty(principalId))
        {
            return;
        }

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        await store.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = tenantId,
                AssignmentId = CryptoService.GenerateResourceId(),
                PrincipalId = principalId,
                RoleName = BuiltInRoles.Owner,
                ResourceType = resourceType,
                ResourceId = resourceId,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );
    }
}
