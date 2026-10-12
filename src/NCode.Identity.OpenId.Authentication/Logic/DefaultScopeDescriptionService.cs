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

using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides the default implementation of <see cref="IScopeDescriptionService"/> that resolves scope descriptions from
/// the tenant's resource servers.
/// </summary>
internal class DefaultScopeDescriptionService(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IScopeDescriptionService
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, string>> DescribeScopesAsync(
        string tenantId,
        IReadOnlyCollection<string> scopeValues,
        CancellationToken cancellationToken
    )
    {
        if (scopeValues.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // Scope values are case-sensitive per RFC 6749.
        var wanted =
            scopeValues as IReadOnlySet<string> ?? scopeValues.ToHashSet(StringComparer.Ordinal);
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);

        // The OIDC runtime endpoints do not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server reads (ADR-0018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();

        // The DBMS pre-filters to enabled scopes matching the wanted values (case-insensitive); the exact
        // case-sensitive match is applied here (ADR-0080).
        var scopes = await resourceServerStore.GetEnabledScopesAsync(
            scopeValues,
            cancellationToken
        );

        foreach (var scope in scopes)
        {
            if (
                !descriptions.ContainsKey(scope.Value)
                && wanted.Contains(scope.Value)
                && !string.IsNullOrEmpty(scope.Description)
            )
            {
                descriptions[scope.Value] = scope.Description;
            }
        }

        return descriptions;
    }
}
