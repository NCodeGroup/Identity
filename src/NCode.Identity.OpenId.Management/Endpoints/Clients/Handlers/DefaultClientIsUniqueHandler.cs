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

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Validates that no OpenID Client already exists with the requested identifier. The check queries the store
/// directly rather than inferring a duplicate from a caught exception.
/// </summary>
[PublicAPI]
internal class DefaultClientIsUniqueHandler(IStoreManagerFactory storeManagerFactory)
    : ICommandHandler<ValidateCreateClientCommand>,
        ISupportMediatorPriority
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateCreateClientCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, client, disposition) = command;

        // short-circuit if an earlier precondition already failed
        if (disposition.HasError)
        {
            return;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var existing = await store.GetOrDefaultAsync(client.ClientId, cancellationToken);
        if (existing is not null)
        {
            disposition.Error ??= new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A client with the specified identifier already exists.",
            };
        }
    }
}
