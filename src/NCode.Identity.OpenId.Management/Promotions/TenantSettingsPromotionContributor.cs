// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides an implementation of <see cref="ITenantPromotionContributor"/> that promotes the tenant's settings
/// document: the source's settings other than the environment-bound ones, then the target's own environment-bound
/// values, then the environment-bound values the request supplies for the target tenant.
/// </summary>
// See ADR-1425.
internal class TenantSettingsPromotionContributor(
    IPromotedSettingsComposer promotedSettingsComposer,
    ISettingSerializer settingSerializer
) : ITenantPromotionContributor
{
    private IPromotedSettingsComposer PromotedSettingsComposer { get; } = promotedSettingsComposer;

    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    /// <inheritdoc />
    public int Order => TenantPromotionOrders.TenantSettings;

    /// <inheritdoc />
    public ValueTask PlanAsync(TenantPromotionContext context, CancellationToken cancellationToken)
    {
        var targetTenantId = context.TargetTenant.TenantId;
        var targetDocument = context.TargetTenant.Settings.Value;
        var promoted = Compose(context);
        var document = promoted.Document;

        foreach (var settingName in promoted.RefusedSettingNames)
        {
            context.AddError(
                new TenantPromotionErrorResource
                {
                    ResourceType = ManagementResourceTypes.Tenant,
                    ResourceId = targetTenantId,
                    Detail =
                        $"The setting '{settingName}' is not bound to the environment, so the request cannot supply it.",
                }
            );
        }

        var settingErrors = SettingSerializer.ValidateSettings(
            document,
            OpenIdSettingScopes.Tenant,
            context.JsonSerializerOptions
        );
        foreach (var settingError in settingErrors)
        {
            context.AddError(
                new TenantPromotionErrorResource
                {
                    ResourceType = ManagementResourceTypes.Tenant,
                    ResourceId = targetTenantId,
                    Detail = settingError.Message,
                }
            );
        }

        context.TargetTenantSettings = document;

        var isUpdated = !JsonElement.DeepEquals(document, targetDocument);
        context.AddChange(
            new TenantPromotionChangeResource
            {
                ResourceType = ManagementResourceTypes.Tenant,
                ResourceId = targetTenantId,
                Action = isUpdated
                    ? TenantPromotionActions.Updated
                    : TenantPromotionActions.Unchanged,
            }
        );

        if (isUpdated)
        {
            context.AddChangedLimits(
                context.TeamNode,
                SettingSerializer.GetChangedLimits(targetDocument, document)
            );
        }

        foreach (var settingName in promoted.UnsetSettingNames)
        {
            context.AddNote(
                new TenantPromotionNoteResource
                {
                    ResourceType = ManagementResourceTypes.Tenant,
                    ResourceId = targetTenantId,
                    Code = TenantPromotionNoteCodes.EnvironmentSettingUnset,
                    Detail =
                        $"The source tenant sets '{settingName}', which belongs to its environment, and the promoted settings do not.",
                    SettingName = settingName,
                }
            );
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask ApplyAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var targetTenant = context.TargetTenant;
        if (JsonElement.DeepEquals(context.TargetTenantSettings, targetTenant.Settings.Value))
        {
            return;
        }

        var store = context.StoreManager.GetStore<ITenantStore>();
        var tenantSettings =
            await store.GetSettingsOrDefaultAsync(targetTenant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The settings of the target tenant '{targetTenant.TenantId}' were not found."
            );

        tenantSettings.Value = context.TargetTenantSettings;

        await store.UpdateSettingsAsync(tenantSettings, cancellationToken);

        var resource = new TenantSettingsResource
        {
            TenantId = tenantSettings.TenantId,
            ConcurrencyToken = tenantSettings.ConcurrencyToken,
            Settings = tenantSettings.Value,
        };

        context.AddAuditEvent(
            new TenantChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = targetTenant.TenantId,
                TeamId = targetTenant.TeamId,
                ResourceId = targetTenant.TenantId,
                ChangeType = ResourceChangeTypes.SettingsUpdated,
                ResourceValues = JsonSerializer.SerializeToElement(
                    resource,
                    JsonSerializerOptions.Web
                ),
                Outcome = AuditOutcome.Success,
            }
        );
    }

    private PromotedSettings Compose(TenantPromotionContext context) =>
        PromotedSettingsComposer.Compose(
            context.SourceTenant.Settings.Value,
            context.TargetTenant.Settings.Value,
            context.GetEnvironmentSettingsOrDefault(
                ManagementResourceTypes.Tenant,
                context.TargetTenant.TenantId
            )
        );
}
