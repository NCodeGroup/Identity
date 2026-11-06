// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Provides the default implementation of <see cref="ITeamClientCoherenceChecker"/>, which checks the team's tenants,
/// then walks the teams beneath it depth first through the <see cref="ITeamDescendantWalker"/>, resolving each team's
/// settings document onto its parent's current and new settings through the <see cref="IEffectiveSettingsResolver"/>
/// and checking that team's tenants through the <see cref="ITenantClientCoherenceChecker"/>. A team whose settings
/// document cannot be read is skipped together with the teams beneath it, and only the settings of the teams on the
/// path to the visited team are held. Tenants are read as an untracked, secret-free projection
/// (<see cref="ITenantStore.GetSettingsPageAsync(string, PageRequest, CancellationToken)"/>).
/// </summary>
// See ADR-1206, ADR-1241, ADR-1426 and ADR-1455.
internal sealed class DefaultTeamClientCoherenceChecker(
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    ITeamDescendantWalker teamDescendantWalker,
    ILogger<DefaultTeamClientCoherenceChecker> logger
) : ITeamClientCoherenceChecker
{
    // The number of tenants read per page when a settings write is checked against a team's tenants' clients.
    private const int TenantPageSize = 100;

    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private ITeamDescendantWalker TeamDescendantWalker { get; } = teamDescendantWalker;
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

        var teamSettings = new TeamSettingsChange(currentTeamSettings, newTeamSettings);

        var teamViolations = await CheckTenantsAsync(
            storeManager,
            teamId,
            teamSettings,
            cancellationToken
        );
        errors.AddRange(teamViolations);

        // The walk visits each parent before its children and returns to a shallower team after a deeper branch, so
        // the settings of the teams on the path from the changed team to the visited team are all that is kept.
        var path = new List<(string TeamId, TeamSettingsChange Settings)>
        {
            (teamId, teamSettings),
        };

        await TeamDescendantWalker.WalkDescendantsAsync(
            teamId,
            async (team, token) =>
            {
                while (
                    path.Count > 0
                    && !string.Equals(
                        path[^1].TeamId,
                        team.ParentTeamId,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    path.RemoveAt(path.Count - 1);
                }

                if (path.Count == 0)
                {
                    return false;
                }

                var parent = path[^1].Settings;

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
                    return false;
                }

                var childSettings = new TeamSettingsChange(currentSettings, newSettings);
                path.Add((team.TeamId, childSettings));

                var childViolations = await CheckTenantsAsync(
                    storeManager,
                    team.TeamId,
                    childSettings,
                    token
                );
                errors.AddRange(childViolations);

                return true;
            },
            cancellationToken
        );

        return errors;
    }

    // Checks each tenant of one team against the team's current and new effective settings.
    private async ValueTask<IReadOnlyList<SettingError>> CheckTenantsAsync(
        IStoreManager storeManager,
        string teamId,
        TeamSettingsChange settings,
        CancellationToken cancellationToken
    )
    {
        List<SettingError> errors = [];
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
                        settings.Current,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                    newTenantSettings = EffectiveSettingsResolver.Resolve(
                        settings.New,
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

        return errors;
    }
}
