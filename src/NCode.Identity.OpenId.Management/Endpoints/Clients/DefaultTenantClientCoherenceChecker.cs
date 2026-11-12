// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="ITenantClientCoherenceChecker"/>, which pages through the
/// tenant's clients under an explicit tenant scope, because the control-plane endpoints that change tenant and platform
/// settings are not scoped to the tenant they address. It reads each client through the untracked settings projection,
/// which carries no secrets, and checks it through the <see cref="IClientCoherenceValidator"/>, which resolves the
/// client's settings onto the tenant's.
/// </summary>
// See ADR-1206 and ADR-1241.
internal sealed class DefaultTenantClientCoherenceChecker(
    IAmbientTenantAccessor ambientTenantAccessor,
    IClientCoherenceValidator clientCoherenceValidator,
    ILogger<DefaultTenantClientCoherenceChecker> logger
) : ITenantClientCoherenceChecker
{
    // The number of clients read per page.
    private const int ClientPageSize = 100;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private ILogger<DefaultTenantClientCoherenceChecker> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ClientCoherenceFinding>> FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string tenantId,
        IReadOnlySettingCollection currentTenantSettings,
        IReadOnlySettingCollection newTenantSettings,
        CancellationToken cancellationToken
    )
    {
        if (!ClientCoherenceValidator.ReadsChangedSetting(currentTenantSettings, newTenantSettings))
        {
            return [];
        }

        List<ClientCoherenceFinding> findings = [];
        var clientStore = storeManager.GetStore<IClientStore>();

        using var scope = AmbientTenantAccessor.BeginScope(tenantId);

        var page = new PageRequest { Limit = ClientPageSize };
        do
        {
            var result = await clientStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var clientSettings in result.Items)
            {
                IReadOnlyList<ClientCoherenceViolation> violations;
                try
                {
                    violations = ClientCoherenceValidator.Validate(
                        newTenantSettings,
                        clientSettings.Value
                    );
                }
                catch (JsonException exception)
                {
                    // A document the settings cannot honor is incoherent before and after the change, so the change
                    // introduces no violation of it.
                    Logger.ClientSettingsUnreadable(
                        tenantId,
                        clientSettings.ClientId,
                        exception.Message
                    );
                    continue;
                }

                if (violations.Count == 0)
                {
                    continue;
                }

                var existing = ClientCoherenceValidator.Validate(
                    currentTenantSettings,
                    clientSettings.Value
                );
                findings.AddRange(
                    violations
                        .ExceptBy(existing.Select(GetIdentity), GetIdentity)
                        .Select(violation => new ClientCoherenceFinding(
                            clientSettings.ClientId,
                            violation
                        ))
                );
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return findings;
    }

    /// <summary>
    /// Identifies a violation by the client setting, the value and the governing setting that it names. Its description
    /// is left out, because it can name an effective value that the change moves, as a lowered lifetime names its
    /// ceiling, so a violation that predates the change is matched however the change moves that value.
    /// </summary>
    // See ADR-1206.
    private static (string SettingName, string Value, string GoverningSettingName) GetIdentity(
        ClientCoherenceViolation violation
    ) => (violation.SettingName, violation.Value, violation.GoverningSettingName);
}
