// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides an implementation of <see cref="ITenantPromotionContributor"/> that promotes the source tenant's
/// non-system resource servers and their non-system scopes. A resource server is matched by its audience identifier,
/// and a created one keeps the source's opaque identifier.
/// </summary>
// See ADR-1425.
internal class ResourceServerPromotionContributor(
    IPromotedSettingsComposer promotedSettingsComposer,
    ISettingSerializer settingSerializer
) : ITenantPromotionContributor
{
    private const int PageLimit = 100;

    private IPromotedSettingsComposer PromotedSettingsComposer { get; } = promotedSettingsComposer;

    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    /// <inheritdoc />
    public int Order => TenantPromotionOrders.ResourceServers;

    /// <inheritdoc />
    public async ValueTask PlanAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var computation = await ComputeAsync(context, cancellationToken);
        MapIdentifiers(context, computation);

        foreach (var promotion in computation.Promotions)
        {
            foreach (var detail in promotion.Errors)
            {
                context.AddError(
                    new TenantPromotionErrorResource
                    {
                        ResourceType = ManagementResourceTypes.ResourceServer,
                        ResourceId = promotion.TargetId,
                        Detail = detail,
                    }
                );
            }

            context.AddChange(
                new TenantPromotionChangeResource
                {
                    ResourceType = ManagementResourceTypes.ResourceServer,
                    ResourceId = promotion.TargetId,
                    Action = promotion.Action,
                }
            );

            foreach (var settingName in promotion.Settings.UnsetSettingNames)
            {
                context.AddNote(
                    new TenantPromotionNoteResource
                    {
                        ResourceType = ManagementResourceTypes.ResourceServer,
                        ResourceId = promotion.TargetId,
                        Code = TenantPromotionNoteCodes.EnvironmentSettingUnset,
                        Detail =
                            $"The source resource server sets '{settingName}', which belongs to its environment, and the promoted settings do not.",
                        SettingName = settingName,
                    }
                );
            }

            if (
                promotion.Action == TenantPromotionActions.Updated
                && promotion.IsSettingsChanged
                && promotion.Target is { } target
            )
            {
                context.AddChangedLimits(
                    context.TargetTenantNode,
                    SettingSerializer.GetChangedLimits(
                        target.Settings.Value,
                        promotion.Settings.Document
                    )
                );
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask ApplyAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var computation = await ComputeAsync(context, cancellationToken);
        MapIdentifiers(context, computation);

        foreach (var promotion in computation.Promotions)
        {
            if (promotion.Action == TenantPromotionActions.Unchanged)
            {
                continue;
            }

            using var scope = context.BeginTargetScope();

            if (promotion.Target is null)
            {
                await CreateAsync(context, promotion, cancellationToken);
            }
            else
            {
                await UpdateAsync(context, promotion, promotion.Target, cancellationToken);
            }
        }
    }

    private static void MapIdentifiers(
        TenantPromotionContext context,
        ResourceServerPromotionComputation computation
    )
    {
        foreach (var (sourceId, targetId) in computation.TargetIdentifiers)
        {
            context.MapIdentifier(ManagementResourceTypes.ResourceServer, sourceId, targetId);
        }
    }

    private static async ValueTask CreateAsync(
        TenantPromotionContext context,
        ResourceServerPromotion promotion,
        CancellationToken cancellationToken
    )
    {
        var store = context.StoreManager.GetStore<IResourceServerStore>();
        var tenantId = context.TargetTenant.TenantId;
        var source = promotion.Source;

        var resourceServer = new PersistedResourceServer
        {
            TenantId = tenantId,
            ResourceServerId = promotion.TargetId,
            Identifier = source.Identifier,
            ConcurrencyToken = string.Empty,
            Name = source.Name,
            IsSystem = false,
            IsDisabled = source.IsDisabled,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = tenantId,
                ResourceServerId = promotion.TargetId,
                Value = promotion.Settings.Document,
            },
            Scopes = source.Scopes.Where(x => !x.IsSystem).Select(CopyScope).ToList(),
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = tenantId,
                ResourceServerId = promotion.TargetId,
                Value = [],
            },
        };

        await store.AddAsync(resourceServer, cancellationToken);

        AddAuditEvent(context, resourceServer, ResourceChangeTypes.Created);
    }

    private static async ValueTask UpdateAsync(
        TenantPromotionContext context,
        ResourceServerPromotion promotion,
        PersistedResourceServer target,
        CancellationToken cancellationToken
    )
    {
        var store = context.StoreManager.GetStore<IResourceServerStore>();
        var source = promotion.Source;

        if (
            !string.Equals(target.Name, source.Name, StringComparison.Ordinal)
            || target.IsDisabled != source.IsDisabled
        )
        {
            target.Name = source.Name;
            target.IsDisabled = source.IsDisabled;
            await store.UpdateAsync(target, cancellationToken);
        }

        if (promotion.IsSettingsChanged)
        {
            target.Settings.Value = promotion.Settings.Document;
            await store.UpdateSettingsAsync(target.Settings, cancellationToken);
        }

        foreach (var scope in promotion.MissingScopes)
        {
            await store.AddScopeAsync(target.ResourceServerId, CopyScope(scope), cancellationToken);
        }

        foreach (var scope in promotion.ChangedScopes)
        {
            await store.UpdateScopeAsync(target.ResourceServerId, scope, cancellationToken);
        }

        AddAuditEvent(context, target, ResourceChangeTypes.Updated);
    }

    private static void AddAuditEvent(
        TenantPromotionContext context,
        PersistedResourceServer resourceServer,
        string changeType
    ) =>
        context.AddAuditEvent(
            new ResourceServerChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = context.TargetTenant.TenantId,
                ResourceId = resourceServer.ResourceServerId,
                ChangeType = changeType,
                ResourceValues = JsonSerializer.SerializeToElement(
                    ToResource(resourceServer),
                    JsonSerializerOptions.Web
                ),
                Outcome = AuditOutcome.Success,
            }
        );

    private static ResourceServerResource ToResource(PersistedResourceServer resourceServer) =>
        new()
        {
            TenantId = resourceServer.TenantId,
            ResourceServerId = resourceServer.ResourceServerId,
            Identifier = resourceServer.Identifier,
            ConcurrencyToken = resourceServer.ConcurrencyToken,
            Name = resourceServer.Name,
            IsSystem = resourceServer.IsSystem,
            IsDisabled = resourceServer.IsDisabled,
            Scopes = resourceServer
                .Scopes.Select(x => new ScopeResource
                {
                    Value = x.Value,
                    Description = x.Description,
                    IsSystem = x.IsSystem,
                })
                .ToList(),
        };

    private static PersistedScope CopyScope(PersistedScope scope) =>
        new()
        {
            Value = scope.Value,
            Description = scope.Description,
            IsSystem = false,
        };

    private async ValueTask<ResourceServerPromotionComputation> ComputeAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var store = context.StoreManager.GetStore<IResourceServerStore>();
        var sources = await GetSourceResourceServersAsync(context, store, cancellationToken);
        var computation = new ResourceServerPromotionComputation();

        foreach (var source in sources)
        {
            using var scope = context.BeginTargetScope();

            var target = await store.GetByIdentifierOrDefaultAsync(
                source.Identifier,
                cancellationToken
            );

            if (source.IsSystem)
            {
                if (target is not null)
                {
                    computation.TargetIdentifiers[source.ResourceServerId] =
                        target.ResourceServerId;
                }

                continue;
            }

            var errors = new List<string>();
            string targetId;
            if (target is not null)
            {
                targetId = target.ResourceServerId;
                if (target.IsSystem)
                {
                    errors.Add(
                        $"The target tenant's resource server '{source.Identifier}' is a system resource server, so the promotion cannot write it."
                    );
                }
            }
            else
            {
                targetId = source.ResourceServerId;
                var holder = await store.GetOrDefaultAsync(targetId, cancellationToken);
                if (holder is not null)
                {
                    errors.Add(
                        $"The target tenant holds the resource server id '{targetId}' under another audience identifier."
                    );
                }
            }

            computation.TargetIdentifiers[source.ResourceServerId] = targetId;

            var promoted = PromotedSettingsComposer.Compose(
                source.Settings.Value,
                target?.Settings.Value,
                context.GetEnvironmentSettingsOrDefault(
                    ManagementResourceTypes.ResourceServer,
                    targetId
                )
            );

            foreach (var settingName in promoted.RefusedSettingNames)
            {
                errors.Add(
                    $"The setting '{settingName}' is not bound to the environment, so the request cannot supply it."
                );
            }

            var settingErrors = SettingSerializer.ValidateSettings(
                promoted.Document,
                OpenIdSettingScopes.ResourceServer,
                context.JsonSerializerOptions
            );
            foreach (var settingError in settingErrors)
            {
                errors.Add(settingError.Message);
            }

            var missingScopes = new List<PersistedScope>();
            var changedScopes = new List<PersistedScope>();
            IReadOnlyCollection<PersistedScope> targetScopes = target?.Scopes ?? [];
            foreach (var sourceScope in source.Scopes.Where(x => !x.IsSystem))
            {
                var targetScope = targetScopes.FirstOrDefault(x =>
                    string.Equals(x.Value, sourceScope.Value, StringComparison.OrdinalIgnoreCase)
                );

                if (targetScope is null)
                {
                    missingScopes.Add(sourceScope);
                }
                else if (
                    !string.Equals(
                        targetScope.Description,
                        sourceScope.Description,
                        StringComparison.Ordinal
                    )
                )
                {
                    changedScopes.Add(
                        new PersistedScope
                        {
                            Value = targetScope.Value,
                            Description = sourceScope.Description,
                            IsSystem = targetScope.IsSystem,
                        }
                    );
                }
            }

            var isSettingsChanged =
                target is not null
                && !JsonElement.DeepEquals(promoted.Document, target.Settings.Value);

            string action;
            if (target is null)
            {
                action = TenantPromotionActions.Created;
            }
            else if (
                !string.Equals(target.Name, source.Name, StringComparison.Ordinal)
                || target.IsDisabled != source.IsDisabled
                || isSettingsChanged
                || missingScopes.Count > 0
                || changedScopes.Count > 0
            )
            {
                action = TenantPromotionActions.Updated;
            }
            else
            {
                action = TenantPromotionActions.Unchanged;
            }

            computation.Promotions.Add(
                new ResourceServerPromotion
                {
                    Source = source,
                    Target = target,
                    TargetId = targetId,
                    Settings = promoted,
                    MissingScopes = missingScopes,
                    ChangedScopes = changedScopes,
                    Action = action,
                    Errors = errors,
                    IsSettingsChanged = isSettingsChanged,
                }
            );
        }

        return computation;
    }

    private static async ValueTask<List<PersistedResourceServer>> GetSourceResourceServersAsync(
        TenantPromotionContext context,
        IResourceServerStore store,
        CancellationToken cancellationToken
    )
    {
        using var scope = context.BeginSourceScope();

        var resourceServers = new List<PersistedResourceServer>();
        string? cursor = null;
        do
        {
            var page = await store.GetPageAsync(
                new PageRequest { Cursor = cursor, Limit = PageLimit },
                cancellationToken
            );

            resourceServers.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        return resourceServers;
    }
}
