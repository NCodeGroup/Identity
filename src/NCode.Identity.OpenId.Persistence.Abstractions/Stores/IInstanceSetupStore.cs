// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists the setup state of an instance, the one
/// <see cref="PersistedInstanceSetup"/> that every replica reads.
/// </summary>
/// <remarks>
/// <see cref="IStore{T}.UpdateAsync"/> throws <see cref="StoreConcurrencyException"/> when the item's
/// <see cref="PersistedInstanceSetup.ConcurrencyToken"/> does not match the stored token, so two replicas
/// can never both complete setup.
/// </remarks>
[PublicAPI]
public interface IInstanceSetupStore : IStore<PersistedInstanceSetup>
{
    /// <summary>
    /// Attempts to get the <see cref="PersistedInstanceSetup"/> from the store.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedInstanceSetup"/> if it exists.</returns>
    ValueTask<PersistedInstanceSetup?> GetOrDefaultAsync(CancellationToken cancellationToken);
}
