// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides the default implementation of <see cref="IClientPrincipalResolver"/>, which recognizes a caller whose
/// subject, derived through the environment's host-configured <c>GetSubjectId</c>, is the principal of the client its
/// <c>client_id</c> claim names in the context's tenant. It answers each caller once per request: the first answer, a
/// client or none, is kept in the request's <see cref="ActingClientMemo"/> and stands for the rest of the request.
/// </summary>
// See ADR-1450 and ADR-1470.
internal class DefaultClientPrincipalResolver(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IClientPrincipalResolver
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    private static bool TryGetCaller(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        out ActingCaller caller
    )
    {
        caller = default;

        var subject = openIdContext.GetSubjectId(user);
        var clientId = user.FindFirstValue(JoseClaimNames.Payload.ClientId);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(clientId))
        {
            return false;
        }

        var tenantId = openIdContext.Tenant.TenantId;
        caller = new ActingCaller(tenantId, subject, clientId);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> GetActingClientOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCaller(openIdContext, user, out var caller))
        {
            return null;
        }

        var memo = ActingClientMemo.Get(openIdContext.Http);
        if (memo.TryRecall(caller, out var remembered))
        {
            return remembered;
        }

        var client = await ReadActingClientAsync(caller, storeManager, cancellationToken);
        memo.Remember(caller, client);
        return client;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> GetActingClientOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCaller(openIdContext, user, out var caller))
        {
            return null;
        }

        var memo = ActingClientMemo.Get(openIdContext.Http);
        if (memo.TryRecall(caller, out var remembered))
        {
            return remembered;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await GetActingClientOrDefaultAsync(
            openIdContext,
            user,
            storeManager,
            cancellationToken
        );
    }

    private async ValueTask<PersistedClient?> ReadActingClientAsync(
        ActingCaller caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var clientStore = storeManager.GetStore<IClientStore>();

        using var scope = AmbientTenantAccessor.BeginScope(caller.TenantId);

        var client = await clientStore.GetOrDefaultAsync(caller.ClientId, cancellationToken);
        if (
            client is null
            || !string.Equals(client.PrincipalId, caller.Subject, StringComparison.Ordinal)
        )
        {
            return null;
        }

        return client;
    }
}
