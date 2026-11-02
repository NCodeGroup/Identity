// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ResourceServers;

/// <summary>
/// Resolves the friendly descriptions defined for scopes on their owning resource servers, so interactive surfaces (the
/// consent screen, the "authorized applications" surface) can present human-readable text instead of raw scope values.
/// </summary>
[PublicAPI]
public interface IScopeDescriptionService
{
    /// <summary>
    /// Resolves the friendly description for each of the given scope values within the specified tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant whose resource servers define the scopes.</param>
    /// <param name="scopeValues">The scope values to resolve descriptions for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing a map from scope
    /// value to its friendly description. Scopes without a defined description (or that are not defined on any resource
    /// server) are absent from the map.</returns>
    ValueTask<IReadOnlyDictionary<string, string>> DescribeScopesAsync(
        string tenantId,
        IReadOnlyCollection<string> scopeValues,
        CancellationToken cancellationToken
    );
}
