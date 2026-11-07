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
/// <c>client_id</c> claim names in the context's tenant.
/// </summary>
// See ADR-1450.
internal class DefaultClientPrincipalResolver(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IClientPrincipalResolver
{
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
        var subject = openIdContext.GetSubjectId(user);
        var clientId = user.FindFirstValue(JoseClaimNames.Payload.ClientId);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var tenantId = openIdContext.Tenant.TenantId;
        var clientStore = storeManager.GetStore<IClientStore>();

        using var scope = AmbientTenantAccessor.BeginScope(tenantId);

        var client = await clientStore.GetOrDefaultAsync(clientId, cancellationToken);
        if (
            client is null
            || !string.Equals(client.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(client.PrincipalId, subject, StringComparison.Ordinal)
        )
        {
            return null;
        }

        return client;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> GetActingClientOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await GetActingClientOrDefaultAsync(
            openIdContext,
            user,
            storeManager,
            cancellationToken
        );
    }
}
