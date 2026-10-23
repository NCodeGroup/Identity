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
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Handles <see cref="ReconcileServerSettingsCommand"/> by checking every stored client of every tenant against the
/// server's effective settings and logging a warning for each client that the settings make incoherent. A change to the
/// server's settings that is not a write, such as a configuration reload or a deployment, cannot be refused the way a
/// write is, so every stored client is checked and each incoherent one is reported, the bootstrap administrator
/// included.
/// </summary>
/// <remarks>
/// A client that fails to authenticate or to be honored at its next request is reported here before that request. The
/// handler only reports: it never changes a client. A tenant that cannot be checked is logged and skipped, and the
/// pass continues. The handler is idempotent, because it reads and logs only.
/// </remarks>
// See ADR-0206 and ADR-0219.
internal sealed class ClientCoherenceReportHandler(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    ILogger<ClientCoherenceReportHandler> logger
) : ICommandHandler<ReconcileServerSettingsCommand>
{
    // The tenants and the clients of a tenant are read in pages, so the pass never holds every one at once.
    private const int PageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private ILogger<ClientCoherenceReportHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ReconcileServerSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();
        var clientStore = storeManager.GetStore<IClientStore>();
        var incoherentClientCount = 0;

        var page = new PageRequest { Limit = PageSize };
        do
        {
            var result = await tenantStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                try
                {
                    incoherentClientCount += await ReportTenantAsync(
                        clientStore,
                        command.Settings,
                        tenantSettings,
                        cancellationToken
                    );
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // One tenant that cannot be checked, say one with a settings document the server's settings
                    // reject, must not stop the pass: the other tenants' clients still need a report.
                    Logger.ClientCoherenceCheckFailed(tenantSettings.TenantId, exception);
                }
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        Logger.ClientCoherenceChecked(command.ServerId, incoherentClientCount);
    }

    // Returns the number of incoherent clients of the tenant.
    private async ValueTask<int> ReportTenantAsync(
        IClientStore clientStore,
        IReadOnlySettingCollection serverSettings,
        PersistedTenantSettings tenantSettings,
        CancellationToken cancellationToken
    )
    {
        var tenantId = tenantSettings.TenantId;
        var effectiveTenantSettings = EffectiveSettingsResolver.Resolve(
            serverSettings,
            OpenIdSettingScopes.Tenant,
            tenantSettings.Value
        );

        // The reconciliation addresses every tenant, so each tenant's clients are read under its own scope.
        using var scope = AmbientTenantAccessor.BeginScope(tenantId);

        var incoherentClientCount = 0;
        var page = new PageRequest { Limit = PageSize };
        do
        {
            var result = await clientStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var clientSettings in result.Items)
            {
                var descriptions = FindViolationDescriptions(
                    effectiveTenantSettings,
                    clientSettings.Value
                );
                if (descriptions.Count == 0)
                    continue;

                incoherentClientCount++;
                var violations = string.Join(" ", descriptions);
                Logger.IncoherentClientReported(tenantId, clientSettings.ClientId, violations);
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return incoherentClientCount;
    }

    // A document the settings cannot honor is reported as a violation too.
    private IReadOnlyList<string> FindViolationDescriptions(
        IReadOnlySettingCollection tenantSettings,
        JsonElement clientSettings
    )
    {
        try
        {
            var violations = ClientCoherenceValidator.Validate(tenantSettings, clientSettings);
            return [.. violations.Select(violation => violation.Description)];
        }
        catch (JsonException exception)
        {
            return [exception.Message];
        }
    }
}
