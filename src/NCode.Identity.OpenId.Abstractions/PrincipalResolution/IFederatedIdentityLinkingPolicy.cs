// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides an abstraction that decides whether a new external connection identity is attached to an existing
/// principal (deterministic account linking) or provisions a brand-new principal. Linking is deterministic, configured,
/// and auditable; two already-established principals are never merged automatically.
/// </summary>
[PublicAPI]
public interface IFederatedIdentityLinkingPolicy
{
    /// <summary>
    /// Decides how a new (not-yet-persisted) authenticated identity is linked.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, whose tenant settings
    /// configure the linking behavior.</param>
    /// <param name="user">The authenticated caller whose new connection identity is being provisioned.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the decision participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the linking
    /// decision.</returns>
    ValueTask<FederatedIdentityLinkDecision> ResolveLinkAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
