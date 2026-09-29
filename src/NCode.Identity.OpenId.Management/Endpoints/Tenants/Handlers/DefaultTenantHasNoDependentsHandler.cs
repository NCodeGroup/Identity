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
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Validates that the OpenID Tenant has no dependent child resources (clients or secrets) before it may be
/// deleted. The check queries the store directly rather than inferring the condition from a caught exception.
/// </summary>
[PublicAPI]
internal class DefaultTenantHasNoDependentsHandler(IStoreManagerFactory storeManagerFactory)
    : ICommandHandler<ValidateDeleteTenantCommand>,
        ISupportMediatorPriority
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateDeleteTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, tenant, disposition) = command;

        // short-circuit if an earlier precondition already failed
        if (disposition.HasError)
        {
            return;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var hasDependents = await store.HasDependentsAsync(tenant.TenantId, cancellationToken);
        if (hasDependents)
        {
            disposition.Error ??= new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail =
                    "The tenant cannot be deleted while it still has dependent clients or secrets.",
            };
        }
    }
}
