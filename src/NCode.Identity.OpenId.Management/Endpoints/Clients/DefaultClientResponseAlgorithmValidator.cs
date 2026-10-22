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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientResponseAlgorithmValidator"/>, which pages through the
/// tenant's clients under an explicit tenant scope, because the control-plane endpoints that change tenant and server
/// settings are not scoped to the tenant they address. It reads each client through the untracked settings projection,
/// which carries no secrets (backlog R21), and merges it through the <see cref="IEffectiveSettingsResolver"/>.
/// </summary>
internal sealed class DefaultClientResponseAlgorithmValidator(
    IAmbientTenantAccessor ambientTenantAccessor,
    IEffectiveSettingsResolver effectiveSettingsResolver
) : IClientResponseAlgorithmValidator
{
    // The number of clients read per page.
    private const int ClientPageSize = 100;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    /// <inheritdoc />
    public async ValueTask<
        IReadOnlyList<ClientResponseAlgorithmFinding>
    > FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection currentTenantSettings,
        IReadOnlySettingCollection newTenantSettings,
        CancellationToken cancellationToken
    )
    {
        List<ClientResponseAlgorithmFinding> findings = [];
        var clientStore = storeManager.GetStore<IClientStore>();

        using var scope = AmbientTenantAccessor.BeginScope(tenantId);

        var page = new PageRequest { Limit = ClientPageSize };
        do
        {
            var result = await clientStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var clientSettings in result.Items)
            {
                var newClientSettings = EffectiveSettingsResolver.Resolve(
                    newTenantSettings,
                    OpenIdSettingScopes.Client,
                    clientSettings.Value
                );

                var violations = ClientResponseAlgorithmRule.FindViolations(newClientSettings);
                if (violations.Count == 0)
                {
                    continue;
                }

                var currentClientSettings = EffectiveSettingsResolver.Resolve(
                    currentTenantSettings,
                    OpenIdSettingScopes.Client,
                    clientSettings.Value
                );
                var existing = ClientResponseAlgorithmRule.FindViolations(currentClientSettings);
                findings.AddRange(
                    violations
                        .Except(existing)
                        .Select(violation => new ClientResponseAlgorithmFinding(
                            clientSettings.ClientId,
                            violation
                        ))
                );
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return findings;
    }
}
