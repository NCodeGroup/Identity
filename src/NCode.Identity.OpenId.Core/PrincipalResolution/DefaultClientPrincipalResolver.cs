// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides the default implementation of <see cref="IClientPrincipalResolver"/>, which recognizes a caller whose
/// subject, derived through the environment's host-configured <c>GetSubjectId</c>, is the principal of the client its
/// <c>client_id</c> claim names in the context's tenant. It answers each caller once per request: the first answer, a
/// client or none, is kept for the rest of the request.
/// </summary>
// See ADR-1466, which keeps the answers in the request's ActingClientMemo.
internal class DefaultClientPrincipalResolver(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IClientPrincipalResolver
{
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

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

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

        return await ReadActingClientAsync(memo, caller, storeManager, cancellationToken);
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
        return await ReadActingClientAsync(memo, caller, storeManager, cancellationToken);
    }

    private async ValueTask<PersistedClient?> ReadActingClientAsync(
        ActingClientMemo memo,
        ActingCaller caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var clientStore = storeManager.GetStore<IClientStore>();

        using var scope = AmbientTenantAccessor.BeginScope(caller.TenantId);

        var stored = await clientStore.GetOrDefaultAsync(caller.ClientId, cancellationToken);
        var client =
            stored is not null
            && string.Equals(stored.PrincipalId, caller.Subject, StringComparison.Ordinal)
                ? stored
                : null;

        memo.Remember(caller, client);
        return client;
    }
}
