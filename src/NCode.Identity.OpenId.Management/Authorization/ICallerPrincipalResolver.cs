// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.OpenId.Contexts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides an abstraction that resolves the authenticated caller of the management API to a stable, server-owned
/// principal identifier. The management API authenticates the caller's own access token, so a caller whose token names
/// no subject but a <c>client_id</c> is a client acting for itself and resolves to that client's principal.
/// </summary>
// See ADR-1419.
internal interface ICallerPrincipalResolver
{
    /// <summary>
    /// Resolves the principal identifier of an already-provisioned caller, without provisioning. A caller that has
    /// never been seen, or a client that is missing or disabled, has no authority.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="caller">The authenticated caller of the management API.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the lookup participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// principal identifier, or <c>null</c> when the caller cannot be resolved to a provisioned principal.</returns>
    ValueTask<string?> ResolveCallerPrincipalIdOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the principal identifier of a caller, provisioning a principal for a person on first sight. A client
    /// acting for itself already has its principal and is never provisioned.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="caller">The authenticated caller of the management API.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the provisioning participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// principal identifier, or an empty string when the caller resolves to no principal.</returns>
    ValueTask<string> ResolveCallerPrincipalIdAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
