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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientCoherenceReporter"/>, which reads one page of a tenant's
/// clients under the tenant's ambient scope through the untracked settings projection, which carries no secrets, and
/// checks each client through the <see cref="IClientCoherenceValidator"/>, which resolves the client's settings onto
/// the tenant's.
/// </summary>
// See ADR-0233.
internal sealed class DefaultClientCoherenceReporter(
    IAmbientTenantAccessor ambientTenantAccessor,
    IClientCoherenceValidator clientCoherenceValidator
) : IClientCoherenceReporter
{
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;

    /// <inheritdoc />
    public async ValueTask<PagedResult<IncoherentClient>> FindIncoherentClientsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection tenantSettings,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var clientStore = storeManager.GetStore<IClientStore>();

        // A caller outside the tenant's request, such as the reconciliation, addresses the tenant by its identifier.
        using var scope = AmbientTenantAccessor.BeginScope(tenantId);

        var result = await clientStore.GetSettingsPageAsync(page, cancellationToken);

        List<IncoherentClient> incoherentClients = [];
        foreach (var clientSettings in result.Items)
        {
            var incoherentClient = FindIncoherentClient(tenantSettings, clientSettings);
            if (incoherentClient is not null)
            {
                incoherentClients.Add(incoherentClient);
            }
        }

        return new PagedResult<IncoherentClient>
        {
            Items = incoherentClients,
            NextCursor = result.NextCursor,
        };
    }

    private IncoherentClient? FindIncoherentClient(
        IReadOnlySettingCollection tenantSettings,
        PersistedClientSettings clientSettings
    )
    {
        try
        {
            var violations = ClientCoherenceValidator.Validate(
                tenantSettings,
                clientSettings.Value
            );
            if (violations.Count == 0)
            {
                return null;
            }

            return new IncoherentClient
            {
                ClientId = clientSettings.ClientId,
                Violations = [.. violations],
            };
        }
        catch (JsonException exception)
        {
            // A document the settings cannot honor is incoherent too.
            return new IncoherentClient
            {
                ClientId = clientSettings.ClientId,
                Violations = [],
                DocumentError = exception.Message,
            };
        }
    }
}
