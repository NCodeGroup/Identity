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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

/// <summary>
/// Provides the default implementation of <see cref="IServerValidator"/>.
/// </summary>
[PublicAPI]
internal class DefaultServerValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        IServerValidator
{
    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        PersistedServer server,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            server,
            Operations.Create,
            "The caller is not authorized to create this server.",
            "Authentication is required to create this server."
        );
        if (error is not null)
        {
            return error;
        }

        var store = storeManager.GetStore<IServerStore>();
        var existing = await store.GetOrDefaultAsync(server.ServerId, cancellationToken);
        if (existing is not null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A server with the specified identifier already exists.",
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        PersistedServer server,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            server,
            Operations.Delete,
            "The caller is not authorized to delete this server.",
            "Authentication is required to delete this server."
        );
        if (error is not null)
        {
            return error;
        }

        var store = storeManager.GetStore<IServerStore>();
        var hasDependents = await store.HasDependentsAsync(server.ServerId, cancellationToken);
        if (hasDependents)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The server cannot be deleted while it still has dependent secrets.",
            };
        }

        return null;
    }
}
