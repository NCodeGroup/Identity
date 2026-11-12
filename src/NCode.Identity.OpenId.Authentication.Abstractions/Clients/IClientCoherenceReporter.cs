// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Finds the stored clients of a tenant that the tenant's effective settings make incoherent: a value a registered
/// <see cref="IClientCoherenceRule"/> does not permit or requires, or a settings document that cannot be honored.
/// </summary>
/// <remarks>
/// The reporter only reads. It reports what it finds and changes no client, so a caller acts on the report in its own
/// right.
/// </remarks>
// See ADR-1245.
[PublicAPI]
public interface IClientCoherenceReporter
{
    /// <summary>
    /// Checks one page of the tenant's stored clients, read under the tenant's ambient scope, and returns the
    /// incoherent ones among them, which may be none, with the cursor of the tenant's next page of clients.
    /// </summary>
    /// <param name="storeManager">The store manager that reads the tenant's clients.</param>
    /// <param name="tenantId">The identifier of the tenant that owns the clients.</param>
    /// <param name="tenantSettings">The effective settings of the tenant.</param>
    /// <param name="page">The page of the tenant's clients to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The incoherent clients among the checked page, and the cursor of the tenant's next page of clients, which is
    /// <c>null</c> when the checked page is the last.
    /// </returns>
    ValueTask<PagedResult<IncoherentClient>> FindIncoherentClientsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection tenantSettings,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
