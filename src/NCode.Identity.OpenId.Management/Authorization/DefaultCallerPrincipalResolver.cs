// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the default implementation of <see cref="ICallerPrincipalResolver"/>. A caller whose token names a subject
/// resolves through <see cref="IPrincipalResolver"/>. The management API authenticates the caller's own access token,
/// so a token that names no subject but a <c>client_id</c> is a client acting for itself and resolves to that client's
/// principal in the ambient tenant; a missing or disabled client holds no authority.
/// </summary>
// See ADR-1419.
internal class DefaultCallerPrincipalResolver(IPrincipalResolver principalResolver)
    : ICallerPrincipalResolver
{
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;

    /// <inheritdoc />
    public async ValueTask<string?> ResolveCallerPrincipalIdOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrEmpty(openIdContext.GetSubjectId(caller)))
        {
            return await PrincipalResolver.ResolvePrincipalIdOrDefaultAsync(
                openIdContext,
                caller,
                storeManager,
                cancellationToken
            );
        }

        return await ResolveClientPrincipalIdOrDefaultAsync(
            caller,
            storeManager,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<string> ResolveCallerPrincipalIdAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrEmpty(openIdContext.GetSubjectId(caller)))
        {
            return await PrincipalResolver.ResolvePrincipalIdAsync(
                openIdContext,
                caller,
                storeManager,
                cancellationToken
            );
        }

        var clientPrincipalId = await ResolveClientPrincipalIdOrDefaultAsync(
            caller,
            storeManager,
            cancellationToken
        );
        return clientPrincipalId ?? string.Empty;
    }

    private static async ValueTask<string?> ResolveClientPrincipalIdOrDefaultAsync(
        ClaimsPrincipal caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var clientId = caller.FindFirstValue(JoseClaimNames.Payload.ClientId);
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var clientStore = storeManager.GetStore<IClientStore>();
        var client = await clientStore.GetOrDefaultAsync(clientId, cancellationToken);
        return client is { IsDisabled: false } ? client.PrincipalId : null;
    }
}
