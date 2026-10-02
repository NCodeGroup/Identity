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
    /// <param name="user">The authenticated caller whose new connection identity is being provisioned.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the decision participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the linking
    /// decision.</returns>
    ValueTask<FederatedIdentityLinkDecision> ResolveLinkAsync(
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
