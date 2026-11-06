// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Provides the default implementation of <see cref="ITeamClientCoherenceChecker"/>, which checks the team's tenants,
/// then walks the teams beneath it from the top down, resolving each team's settings document onto its parent's
/// current and new settings through the <see cref="IEffectiveSettingsResolver"/> and checking that team's tenants
/// through the <see cref="ITenantClientCoherenceChecker"/>. Tenants are read as an untracked, secret-free projection
/// (<see cref="ITenantStore.GetSettingsPageAsync(string, PageRequest, CancellationToken)"/>).
/// </summary>
// See ADR-1206, ADR-1241 and ADR-1426.
internal sealed class DefaultTeamClientCoherenceChecker(
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    ILogger<DefaultTeamClientCoherenceChecker> logger
) : ITeamClientCoherenceChecker
{
    // The number of tenants read per page when a settings write is checked against a team's tenants' clients.
    private const int TenantPageSize = 100;

    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private ILogger<DefaultTeamClientCoherenceChecker> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SettingError>> FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string teamId,
        IReadOnlySettingCollection currentTeamSettings,
        IReadOnlySettingCollection newTeamSettings,
        CancellationToken cancellationToken
    )
    {
        List<SettingError> errors = [];

        await CheckTenantsAsync(
            teamId,
            currentTeamSettings,
            newTeamSettings,
            storeManager,
            errors,
            cancellationToken
        );

        var descendants = await storeManager
            .GetStore<ITeamStore>()
            .GetDescendantsAsync(teamId, cancellationToken);

        // A parent precedes its children, so a team's parent is always resolved, or skipped, before the team is read.
        // A null entry marks a team whose settings document cannot be honored, or that sits beneath such a team.
        var settingsByTeamId = new Dictionary<
            string,
            (IReadOnlySettingCollection Current, IReadOnlySettingCollection New)?
        >(StringComparer.OrdinalIgnoreCase)
        {
            [teamId] = (currentTeamSettings, newTeamSettings),
        };

        foreach (var team in descendants)
        {
            if (
                team.ParentTeamId is null
                || !settingsByTeamId.TryGetValue(team.ParentTeamId, out var parentSettings)
                || parentSettings is not { } parent
            )
            {
                settingsByTeamId[team.TeamId] = null;
                continue;
            }

            IReadOnlySettingCollection currentSettings;
            IReadOnlySettingCollection newSettings;
            try
            {
                currentSettings = EffectiveSettingsResolver.Resolve(
                    parent.Current,
                    OpenIdSettingScopes.Team,
                    team.Settings.Value
                );
                newSettings = EffectiveSettingsResolver.Resolve(
                    parent.New,
                    OpenIdSettingScopes.Team,
                    team.Settings.Value
                );
            }
            catch (JsonException exception)
            {
                Logger.TeamSettingsUnreadable(team.TeamId, exception.Message);
                settingsByTeamId[team.TeamId] = null;
                continue;
            }

            settingsByTeamId[team.TeamId] = (currentSettings, newSettings);

            await CheckTenantsAsync(
                team.TeamId,
                currentSettings,
                newSettings,
                storeManager,
                errors,
                cancellationToken
            );
        }

        return errors;
    }

    // Checks each tenant of one team against the team's current and new effective settings.
    private async ValueTask CheckTenantsAsync(
        string teamId,
        IReadOnlySettingCollection currentTeamSettings,
        IReadOnlySettingCollection newTeamSettings,
        IStoreManager storeManager,
        List<SettingError> errors,
        CancellationToken cancellationToken
    )
    {
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            var result = await tenantStore.GetSettingsPageAsync(teamId, page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                IReadOnlySettingCollection currentTenantSettings;
                IReadOnlySettingCollection newTenantSettings;
                try
                {
                    currentTenantSettings = EffectiveSettingsResolver.Resolve(
                        currentTeamSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                    newTenantSettings = EffectiveSettingsResolver.Resolve(
                        newTeamSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                }
                catch (JsonException exception)
                {
                    Logger.TenantSettingsUnreadable(tenantSettings.TenantId, exception.Message);
                    continue;
                }

                var findings = await TenantClientCoherenceChecker.FindIntroducedViolationsAsync(
                    storeManager,
                    tenantSettings.TenantId,
                    currentTenantSettings,
                    newTenantSettings,
                    cancellationToken
                );

                errors.AddRange(
                    findings.Select(finding => new SettingError(
                        finding.Violation.GoverningSettingName,
                        $"The change would make the client '{finding.ClientId}' of tenant '{tenantSettings.TenantId}' incoherent: {finding.Violation.Description}"
                    ))
                );
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }
}
