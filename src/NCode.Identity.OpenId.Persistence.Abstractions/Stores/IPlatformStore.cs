// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedPlatform"/> instances.
/// </summary>
[PublicAPI]
public interface IPlatformStore : IStore<PersistedPlatform>
{
    /// <summary>
    /// Gets a single page of <see cref="PersistedPlatform"/> instances ordered by their identifier, using keyset
    /// (cursor) pagination.
    /// </summary>
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of
    /// <see cref="PersistedPlatform"/> instances and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedPlatform>> GetPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedPlatform"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="platformId">The identifier of the <see cref="PersistedPlatform"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedPlatform"/> instance matching the specified entity if it exists.</returns>
    ValueTask<PersistedPlatform?> GetOrDefaultAsync(
        string platformId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedPlatformSettings"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation. </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedPlatformSettings"/> for the specified identifier.</returns>
    ValueTask<PersistedPlatformSettings?> GetSettingsOrDefaultAsync(
        string platformId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedPlatformSecrets"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedPlatformSecrets"/> for the specified identifier.</returns>
    ValueTask<PersistedPlatformSecrets?> GetSecretsOrDefaultAsync(
        string platformId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the JSON settings for an OpenId Server in the store. It clears the server's reconciled settings
    /// fingerprint, because the settings it was computed from changed, and keeps the reconciled settings for the next
    /// reconciliation to compare with, unless the fingerprint was already clear: the settings of an earlier change
    /// were never reconciled.
    /// </summary>
    /// <param name="persistedServerSettings">The <see cref="PersistedPlatformSettings"/> instance to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSettingsAsync(
        PersistedPlatformSettings persistedServerSettings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records the server's <see cref="PersistedPlatform.ReconciledSettingsFingerprint"/> and
    /// <see cref="PersistedPlatform.ReconciledSettings"/> and nothing else, so the write never overwrites a concurrent
    /// change to the server. The server's
    /// <see cref="BasePersistedPlatformResource.ConcurrencyToken"/> must match the stored one.
    /// </summary>
    /// <param name="persistedPlatform">The <see cref="PersistedPlatform"/> instance whose reconciled settings fingerprint and reconciled settings are recorded.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    /// <exception cref="StoreConcurrencyException">Thrown when the server's concurrency token does not match the stored one.</exception>
    ValueTask UpdateReconciledSettingsAsync(
        PersistedPlatform persistedPlatform,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a single <see cref="PersistedSecret"/> for an OpenId Server by its identifier.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="secretId">The identifier of the secret to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedSecret"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedSecret?> GetSecretOrDefaultAsync(
        string platformId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Adds a new secret to an OpenId Server and bumps the server's secrets concurrency token so that any
    /// running server instance refreshes its secret collection.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddSecretAsync(
        string platformId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the metadata of an existing secret for an OpenId Server (key material is immutable) and bumps
    /// the server's secrets concurrency token so that any running server instance refreshes its secret collection.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> whose metadata is updated. Its
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is checked for optimistic concurrency.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSecretAsync(
        string platformId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a secret from an OpenId Server and bumps the server's secrets concurrency token so that any
    /// running server instance refreshes its secret collection.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="secretId">The identifier of the secret to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if a secret was removed; otherwise <c>false</c> when no matching secret existed.</returns>
    ValueTask<bool> RemoveSecretAsync(
        string platformId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes an OpenId Server from the store. The removal is rejected while the server still has dependent
    /// child resources (any secrets); those dependents must be removed first.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if the server was removed; otherwise <c>false</c> when no matching server existed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the server still has dependent child resources.</exception>
    ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether an OpenId Server still has dependent child resources (any secrets) that would block
    /// its removal.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenId Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// when the server has dependent child resources; otherwise <c>false</c> (including when no such server exists).</returns>
    ValueTask<bool> HasDependentsAsync(string platformId, CancellationToken cancellationToken);
}
