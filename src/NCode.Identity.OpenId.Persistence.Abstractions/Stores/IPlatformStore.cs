// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists the platform, the one <see cref="PersistedPlatform"/> of a
/// deployment.
/// </summary>
[PublicAPI]
public interface IPlatformStore : IStore<PersistedPlatform>
{
    /// <summary>
    /// Attempts to get the <see cref="PersistedPlatform"/> from the store.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedPlatform"/> if it exists.</returns>
    ValueTask<PersistedPlatform?> GetOrDefaultAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get the <see cref="PersistedPlatformSettings"/> from the store.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation. </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedPlatformSettings"/> if the platform exists.</returns>
    ValueTask<PersistedPlatformSettings?> GetSettingsOrDefaultAsync(
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the JSON settings of the platform in the store.
    /// </summary>
    /// <param name="persistedPlatformSettings">The <see cref="PersistedPlatformSettings"/> instance to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    /// <exception cref="StoreConcurrencyException">Thrown when the settings' concurrency token does not match the stored one.</exception>
    ValueTask UpdateSettingsAsync(
        PersistedPlatformSettings persistedPlatformSettings,
        CancellationToken cancellationToken
    );
}
