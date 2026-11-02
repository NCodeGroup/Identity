// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Checks a change to the settings above a tenant's clients against every client coherence rule, so a change never
/// leaves a stored client registering a value the changed settings do not permit.
/// </summary>
// See ADR-1206.
internal interface ITenantClientCoherenceChecker
{
    /// <summary>
    /// Walks no client when no setting a coherence rule reads changed. Otherwise merges each client of a tenant onto
    /// the tenant's effective settings before and after the change, as the runtime merges it, and returns only the
    /// violations the change introduces. A mismatch that predates the change is not returned, so it never blocks a
    /// write the change does not affect. A violation that predates the change is matched by the client setting, the
    /// value and the governing setting that it names, not by its description, which can name an effective value that
    /// the change moves. A client whose settings document the settings vocabulary cannot honor is logged and skipped,
    /// because it is incoherent before and after the change.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenant's clients.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant whose clients to check.</param>
    /// <param name="currentTenantSettings">The tenant's effective settings before the change.</param>
    /// <param name="newTenantSettings">The tenant's effective settings after the change.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ClientCoherenceFinding"/> instances, or an empty list.</returns>
    ValueTask<IReadOnlyList<ClientCoherenceFinding>> FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection currentTenantSettings,
        IReadOnlySettingCollection newTenantSettings,
        CancellationToken cancellationToken
    );
}
