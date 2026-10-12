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
using NCode.Identity.Secrets.Persistence.DataContracts;
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
    /// Gets the scopes owned by the tenant's <b>enabled</b> resource servers, optionally pre-filtered to a set of
    /// scope values. The DBMS applies the enabled filter (and, when provided, a case-insensitive value pre-filter);
    /// callers still apply the case-sensitive match required by RFC 6749.
    /// </summary>
    /// <param name="scopeValues">The scope values to pre-filter by (matched case-insensitively), or <c>null</c> to
    /// return every enabled scope.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the matching
    /// scopes (values may repeat across resource servers).</returns>
    ValueTask<IReadOnlyCollection<PersistedScope>> GetEnabledScopesAsync(
        IReadOnlyCollection<string>? scopeValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the audience identifiers of the tenant's <b>enabled</b> resource servers.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the enabled
    /// audience identifiers.</returns>
    ValueTask<IReadOnlyCollection<string>> GetEnabledIdentifiersAsync(
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets lightweight scope projections for the tenant's <b>enabled</b> resource servers that own at least one of
    /// the specified scope values. The DBMS applies the enabled filter and a case-insensitive owns-any-scope
    /// pre-filter; callers apply the case-sensitive match required by RFC 6749.
    /// </summary>
    /// <param name="scopeValues">The scope values to pre-filter by (matched case-insensitively).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the matching
    /// projections.</returns>
    ValueTask<
        IReadOnlyCollection<ResourceServerScopeProjection>
    > GetEnabledScopeProjectionsByScopesAsync(
        IReadOnlyCollection<string> scopeValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets lightweight scope projections for the tenant's <b>enabled</b> resource servers that are either
    /// system-owned or whose opaque identifier is in the specified set. This serves the allowed-scopes computation,
    /// where system resource servers are implicitly available and non-system ones require a client grant.
    /// </summary>
    /// <param name="resourceServerIds">The opaque identifiers of the resource servers the client holds grants for
    /// (matched case-insensitively).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the matching
    /// projections.</returns>
    ValueTask<
        IReadOnlyCollection<ResourceServerScopeProjection>
    > GetEnabledScopeProjectionsBySystemOrIdsAsync(
        IReadOnlyCollection<string> resourceServerIds,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Finds the tenant's <b>enabled</b> resource servers (with their settings and secrets) that own at least one of
    /// the specified scope values, optionally narrowed to a set of audience identifiers (RFC 8707 resource
    /// indicators). The DBMS applies the enabled filter, a case-insensitive owns-any-scope pre-filter, and (when
    /// provided) a case-insensitive identifier pre-filter; callers apply the case-sensitive matches.
    /// </summary>
    /// <param name="scopeValues">The scope values to pre-filter by (matched case-insensitively).</param>
    /// <param name="identifiers">The audience identifiers to narrow to (matched case-insensitively), or <c>null</c>
    /// to not narrow by identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the matching
    /// resource servers ordered by audience identifier.</returns>
    ValueTask<IReadOnlyCollection<PersistedResourceServer>> FindEnabledByScopesAsync(
        IReadOnlyCollection<string> scopeValues,
        IReadOnlyCollection<string>? identifiers,
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

    /// <summary>
    /// Updates the JSON settings of an existing resource server, enforcing optimistic concurrency against the
    /// settings concurrency token and bumping it on success.
    /// </summary>
    /// <param name="persistedResourceServerSettings">The <see cref="PersistedResourceServerSettings"/> to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSettingsAsync(
        PersistedResourceServerSettings persistedResourceServerSettings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the collection of secrets owned by a resource server.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedResourceServerSecrets"/> if the resource server was found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedResourceServerSecrets?> GetSecretsOrDefaultAsync(
        string resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a single secret owned by a resource server.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="secretId">The opaque identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedResourceServerSecret"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedResourceServerSecret?> GetSecretOrDefaultAsync(
        string resourceServerId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Adds a secret to a resource server and bumps the resource server's secrets concurrency token.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddSecretAsync(
        string resourceServerId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the metadata of an existing secret on a resource server and bumps the resource server's secrets
    /// concurrency token. Key material is immutable once generated.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> whose metadata is updated.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSecretAsync(
        string resourceServerId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a secret from a resource server and bumps the resource server's secrets concurrency token.
    /// </summary>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="secretId">The opaque identifier of the secret to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when a
    /// secret was removed; otherwise <c>false</c>.</returns>
    ValueTask<bool> RemoveSecretAsync(
        string resourceServerId,
        string secretId,
        CancellationToken cancellationToken
    );
}
