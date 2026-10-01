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
/// Provides an abstraction for a store which persists <see cref="PersistedResourceServer"/> instances (APIs, in Auth0
/// terms) and their owned scopes.
/// </summary>
[PublicAPI]
public interface IResourceServerStore : IStore<PersistedResourceServer>
{
    /// <summary>
    /// Gets a single page of <see cref="PersistedResourceServer"/> instances ordered by their identifier, using
    /// keyset (cursor) pagination.
    /// </summary>
    /// <param name="cursor">The opaque cursor returned by a previous call that fetches the next page, or <c>null</c>
    /// to fetch the first page.</param>
    /// <param name="limit">The maximum number of items to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the
    /// cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedResourceServer>> GetPageAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedResourceServer"/> from the store by its opaque identifier.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedResourceServer"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedResourceServer?> GetOrDefaultAsync(
        string resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedResourceServer"/> from the store by its audience identifier.
    /// </summary>
    /// <param name="identifier">The audience identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedResourceServer"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedResourceServer?> GetByIdentifierOrDefaultAsync(
        string identifier,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Determines whether any client grant still references the specified resource server.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when at
    /// least one client grant references the resource server; otherwise <c>false</c>.</returns>
    ValueTask<bool> HasDependentsAsync(
        string resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a resource server (and its scopes) from the store.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(string resourceServerId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a scope to a resource server and bumps the resource server's scopes concurrency token.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="persistedScope">The <see cref="PersistedScope"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddScopeAsync(
        string resourceServerId,
        PersistedScope persistedScope,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the metadata of an existing scope on a resource server and bumps the resource server's scopes
    /// concurrency token.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="persistedScope">The <see cref="PersistedScope"/> whose metadata is updated.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateScopeAsync(
        string resourceServerId,
        PersistedScope persistedScope,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a scope from a resource server and bumps the resource server's scopes concurrency token.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="scopeValue">The value of the scope to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when a
    /// scope was removed; otherwise <c>false</c>.</returns>
    ValueTask<bool> RemoveScopeAsync(
        string resourceServerId,
        string scopeValue,
        CancellationToken cancellationToken
    );
}
