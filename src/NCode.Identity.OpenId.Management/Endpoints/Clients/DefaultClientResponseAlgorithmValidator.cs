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

using System.Text.Json;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientResponseAlgorithmValidator"/>, which pages through the
/// tenant's clients under an explicit tenant scope, because the control-plane endpoints that change tenant and server
/// settings are not scoped to the tenant they address.
/// </summary>
internal sealed class DefaultClientResponseAlgorithmValidator(
    IAmbientTenantAccessor ambientTenantAccessor,
    ISecurityProfileCatalog securityProfileCatalog
) : IClientResponseAlgorithmValidator
{
    // The number of clients read per page.
    private const int ClientPageSize = 100;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <inheritdoc />
    public async ValueTask<
        IReadOnlyList<ClientResponseAlgorithmFinding>
    > FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection currentTenantSettings,
        IReadOnlySettingCollection newTenantSettings,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    )
    {
        List<ClientResponseAlgorithmFinding> findings = [];
        var clientStore = storeManager.GetStore<IClientStore>();

        using var scope = AmbientTenantAccessor.BeginScope(tenantId);

        var page = new PageRequest { Limit = ClientPageSize };
        do
        {
            var result = await clientStore.GetPageAsync(page, cancellationToken);
            foreach (var client in result.Items)
            {
                var clientSettings = SecurityProfileCatalog.Expand(
                    client.Settings.Value,
                    OpenIdSettingScopes.Client,
                    jsonOptions
                );

                var violations = ClientResponseAlgorithmRule.FindViolations(
                    newTenantSettings.Merge(clientSettings)
                );
                if (violations.Count == 0)
                {
                    continue;
                }

                var existing = ClientResponseAlgorithmRule.FindViolations(
                    currentTenantSettings.Merge(clientSettings)
                );
                findings.AddRange(
                    violations
                        .Except(existing)
                        .Select(violation => new ClientResponseAlgorithmFinding(
                            client.ClientId,
                            violation
                        ))
                );
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return findings;
    }
}
