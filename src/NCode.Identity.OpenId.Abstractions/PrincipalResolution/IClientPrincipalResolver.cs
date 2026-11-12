// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides an abstraction that recognizes a token in which a client acts for itself: its subject is the principal of
/// the client its <c>client_id</c> claim names in the context's tenant. An implementation answers each caller, which
/// is the context's tenant, the subject and the <c>client_id</c> claim, once per request: its first answer, a client
/// or none, stands for the rest of the request, and a change the request commits, such as deleting the client, does
/// not alter it.
/// </summary>
// See ADR-1466.
[PublicAPI]
public interface IClientPrincipalResolver
{
    /// <summary>
    /// Gets the client that a caller acts as, when the caller is a client acting for itself. A disabled client is
    /// returned; the caller decides what it may do.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, whose tenant owns the
    /// client.</param>
    /// <param name="user">The authenticated caller.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the lookup participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClient"/> that the caller acts as, or <c>null</c> when the caller is not a client acting for
    /// itself.</returns>
    ValueTask<PersistedClient?> GetActingClientOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the client that a caller acts as, when the caller is a client acting for itself, managing its own store
    /// unit of work. A disabled client is returned; the caller decides what it may do.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, whose tenant owns the
    /// client.</param>
    /// <param name="user">The authenticated caller.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClient"/> that the caller acts as, or <c>null</c> when the caller is not a client acting for
    /// itself.</returns>
    ValueTask<PersistedClient?> GetActingClientOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    );
}
