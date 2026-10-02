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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedFederatedPrincipal"/> instances: the global,
/// server-owned human actors that authority references.
/// </summary>
[PublicAPI]
public interface IFederatedPrincipalStore : IStore
{
    /// <summary>
    /// Adds a new federated principal to the store.
    /// </summary>
    /// <param name="principal">The <see cref="PersistedFederatedPrincipal"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(PersistedFederatedPrincipal principal, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get a federated principal from the store by its opaque identifier.
    /// </summary>
    /// <param name="principalId">The opaque identifier of the principal.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedFederatedPrincipal"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedFederatedPrincipal?> GetOrDefaultAsync(
        string principalId,
        CancellationToken cancellationToken
    );
}
