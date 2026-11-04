// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Handles <see cref="ReconcilePlatformSettingsCommand"/> by checking the stored clients of the tenants a change to the
/// server's settings affects against the server's effective settings and logging a warning for each client that the
/// settings make incoherent. When the command carries the settings it was last reconciled against, a tenant's clients
/// are checked only if a setting that a coherence rule reads has a different effective value for the tenant; without
/// them every tenant's clients are checked. A change to the server's settings that is not a write, such as a
/// configuration reload or a deployment, cannot be refused the way a write is, so each incoherent stored client is
/// reported, the bootstrap administrator included.
/// </summary>
/// <remarks>
/// A client that fails to authenticate or to be honored at its next request is reported here before that request. The
/// <see cref="IClientCoherenceReporter"/> checks each page of each tenant's clients. The handler only reports: it never
/// changes a client. A tenant that cannot be checked is logged and skipped, and the pass continues. The handler is
/// idempotent, because it reads and logs only.
/// </remarks>
// See ADR-1206, ADR-1219 and ADR-1234.
internal sealed class ClientCoherenceReportHandler(
    IStoreManagerFactory storeManagerFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    IClientCoherenceReporter clientCoherenceReporter,
    ILogger<ClientCoherenceReportHandler> logger
) : ICommandHandler<ReconcilePlatformSettingsCommand>
{
    // The tenants and the clients of a tenant are read in pages, so the pass never holds every one at once.
    private const int PageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private IClientCoherenceReporter ClientCoherenceReporter { get; } = clientCoherenceReporter;
    private ILogger<ClientCoherenceReportHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ReconcilePlatformSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();
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
                        storeManager,
                        command,
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

        Logger.ClientCoherenceChecked(command.PlatformId, incoherentClientCount);
    }

    // Returns the number of incoherent clients of the tenant.
    private async ValueTask<int> ReportTenantAsync(
        IStoreManager storeManager,
        ReconcilePlatformSettingsCommand command,
        PersistedTenantSettings tenantSettings,
        CancellationToken cancellationToken
    )
    {
        var tenantId = tenantSettings.TenantId;
        var effectiveTenantSettings = EffectiveSettingsResolver.Resolve(
            command.Settings,
            OpenIdSettingScopes.Tenant,
            tenantSettings.Value
        );

        // A tenant whose settings no coherence rule reads differently cannot hold a client that the change made
        // incoherent. See ADR-1234.
        if (command.PreviousSettings is { } previousServerSettings)
        {
            var previousTenantSettings = EffectiveSettingsResolver.Resolve(
                previousServerSettings,
                OpenIdSettingScopes.Tenant,
                tenantSettings.Value
            );
            if (
                !ClientCoherenceValidator.ReadsChangedSetting(
                    previousTenantSettings,
                    effectiveTenantSettings
                )
            )
                return 0;
        }

        var incoherentClientCount = 0;
        var page = new PageRequest { Limit = PageSize };
        do
        {
            var result = await ClientCoherenceReporter.FindIncoherentClientsAsync(
                storeManager,
                tenantId,
                effectiveTenantSettings,
                page,
                cancellationToken
            );
            foreach (var client in result.Items)
            {
                incoherentClientCount++;
                var description = Describe(client);
                Logger.IncoherentClientReported(tenantId, client.ClientId, description);
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return incoherentClientCount;
    }

    // A document the settings cannot honor is described by its reason.
    private static string Describe(IncoherentClient client) =>
        client.DocumentError
        ?? string.Join(" ", client.Violations.Select(violation => violation.Description));
}
