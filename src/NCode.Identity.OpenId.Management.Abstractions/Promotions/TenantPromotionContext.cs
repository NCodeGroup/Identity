// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Carries the shared state of one tenant promotion: the source and target tenants, the unit of work every contributor
/// enlists in, the document the target holds once promoted, and what planning records for the report, the audit trail
/// and the authority checks (changes, notes, errors, changed limits, required creation authorities and the mapping
/// of source identifiers to target identifiers).
/// </summary>
// See ADR-1425.
[PublicAPI]
public sealed class TenantPromotionContext
{
    private List<TenantPromotionChangeResource> ChangeList { get; } = [];
    private List<TenantPromotionNoteResource> NoteList { get; } = [];
    private List<TenantPromotionErrorResource> ErrorList { get; } = [];
    private List<ResourceChangeAuditEvent> AuditEventList { get; } = [];
    private List<TenantPromotionLimitChange> ChangedLimitList { get; } = [];
    private List<ISupportTenantId> RequiredCreateAuthorityList { get; } = [];
    private Dictionary<(string ResourceType, string SourceId), string> Identifiers { get; } = [];

    /// <summary>
    /// Gets the tenant the promotion reads its configuration from.
    /// </summary>
    public required PersistedTenant SourceTenant { get; init; }

    /// <summary>
    /// Gets the tenant the promotion writes its configuration to.
    /// </summary>
    public required PersistedTenant TargetTenant { get; init; }

    /// <summary>
    /// Gets the team's authority node, the node above the target tenant's settings document.
    /// </summary>
    public required ISupportTenantId TeamNode { get; init; }

    /// <summary>
    /// Gets the target tenant's authority node, the node above its clients' and resource servers' documents.
    /// </summary>
    public required ISupportTenantId TargetTenantNode { get; init; }

    /// <summary>
    /// Gets the unit of work that all contributors share. Contributors obtain their stores from it and must not save
    /// it; the promoter saves after each contributor.
    /// </summary>
    public required IStoreManager StoreManager { get; init; }

    /// <summary>
    /// Gets the accessor that opens the ambient tenant scope tenant-bound stores read.
    /// </summary>
    public required IAmbientTenantAccessor AmbientTenantAccessor { get; init; }

    /// <summary>
    /// Gets the options used to read and write settings documents.
    /// </summary>
    public required JsonSerializerOptions JsonSerializerOptions { get; init; }

    /// <summary>
    /// Gets the environment-bound setting values the request supplies, by the resource that receives them.
    /// </summary>
    public required IReadOnlyList<TenantPromotionEnvironmentSettings> EnvironmentSettings { get; init; }

    /// <summary>
    /// Gets or sets the settings document the target tenant holds once promoted.
    /// </summary>
    public required JsonElement TargetTenantSettings { get; set; }

    /// <summary>
    /// Gets the changes the promotion makes, in the order they were recorded.
    /// </summary>
    public IReadOnlyList<TenantPromotionChangeResource> Changes => ChangeList;

    /// <summary>
    /// Gets the notes the promotion records, in the order they were recorded.
    /// </summary>
    public IReadOnlyList<TenantPromotionNoteResource> Notes => NoteList;

    /// <summary>
    /// Gets the errors that refuse the promotion, in the order they were recorded.
    /// </summary>
    public IReadOnlyList<TenantPromotionErrorResource> Errors => ErrorList;

    /// <summary>
    /// Gets the audit events the contributors stage, in the order they were added.
    /// </summary>
    public IReadOnlyList<ResourceChangeAuditEvent> AuditEvents => AuditEventList;

    /// <summary>
    /// Gets the limits the promoted documents change, each with the node whose <c>Update</c> authority the change
    /// needs.
    /// </summary>
    public IReadOnlyList<TenantPromotionLimitChange> ChangedLimits => ChangedLimitList;

    /// <summary>
    /// Gets the nodes at which the caller needs <c>Create</c> authority because the promotion changes an existing
    /// target client whose principal holds role assignments there.
    /// </summary>
    public IReadOnlyList<ISupportTenantId> RequiredCreateAuthorities => RequiredCreateAuthorityList;

    /// <summary>
    /// Opens the ambient tenant scope of the source tenant. Contributors open it around every tenant-bound read of the
    /// source tenant.
    /// </summary>
    /// <returns>The scope, which ends when disposed.</returns>
    public IDisposable BeginSourceScope() =>
        AmbientTenantAccessor.BeginScope(SourceTenant.TenantId);

    /// <summary>
    /// Opens the ambient tenant scope of the target tenant. Contributors open it around every tenant-bound read and
    /// write of the target tenant.
    /// </summary>
    /// <returns>The scope, which ends when disposed.</returns>
    public IDisposable BeginTargetScope() =>
        AmbientTenantAccessor.BeginScope(TargetTenant.TenantId);

    /// <summary>
    /// Gets the environment-bound settings the request supplies for a resource of the target tenant.
    /// </summary>
    /// <param name="resourceType">The type of the resource.</param>
    /// <param name="resourceId">The identifier of the resource in the target tenant.</param>
    /// <returns>The first matching entry's settings, or <see langword="null"/> when the request supplies none.</returns>
    public JsonElement? GetEnvironmentSettingsOrDefault(string resourceType, string resourceId)
    {
        foreach (var entry in EnvironmentSettings)
        {
            if (
                string.Equals(entry.ResourceType, resourceType, StringComparison.Ordinal)
                && string.Equals(entry.ResourceId, resourceId, StringComparison.Ordinal)
            )
            {
                return entry.Settings;
            }
        }

        return null;
    }

    /// <summary>
    /// Records a change the promotion makes.
    /// </summary>
    /// <param name="change">The change.</param>
    public void AddChange(TenantPromotionChangeResource change)
    {
        ArgumentNullException.ThrowIfNull(change);

        ChangeList.Add(change);
    }

    /// <summary>
    /// Records a note.
    /// </summary>
    /// <param name="note">The note.</param>
    public void AddNote(TenantPromotionNoteResource note)
    {
        ArgumentNullException.ThrowIfNull(note);

        NoteList.Add(note);
    }

    /// <summary>
    /// Records an error that refuses the promotion.
    /// </summary>
    /// <param name="error">The error.</param>
    public void AddError(TenantPromotionErrorResource error)
    {
        ArgumentNullException.ThrowIfNull(error);

        ErrorList.Add(error);
    }

    /// <summary>
    /// Stages an audit event that the promoter records once the contributor's writes are saved.
    /// </summary>
    /// <param name="auditEvent">The audit event.</param>
    public void AddAuditEvent(ResourceChangeAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        AuditEventList.Add(auditEvent);
    }

    /// <summary>
    /// Records the limits a promoted document changes in an existing target document.
    /// </summary>
    /// <param name="authorityAbove">The node whose <c>Update</c> authority the change needs.</param>
    /// <param name="settingNames">The names of the changed limits. Nothing is recorded when the list is empty.</param>
    public void AddChangedLimits(
        ISupportTenantId authorityAbove,
        IReadOnlyList<string> settingNames
    )
    {
        ArgumentNullException.ThrowIfNull(authorityAbove);
        ArgumentNullException.ThrowIfNull(settingNames);

        if (settingNames.Count == 0)
            return;

        ChangedLimitList.Add(
            new TenantPromotionLimitChange
            {
                AuthorityAbove = authorityAbove,
                SettingNames = [.. settingNames],
            }
        );
    }

    /// <summary>
    /// Records a node at which the caller needs <c>Create</c> authority because the promotion changes an existing
    /// target client whose principal holds role assignments there.
    /// </summary>
    /// <param name="node">The node.</param>
    public void AddRequiredCreateAuthority(ISupportTenantId node)
    {
        ArgumentNullException.ThrowIfNull(node);

        RequiredCreateAuthorityList.Add(node);
    }

    /// <summary>
    /// Records the identifier a source resource has in the target tenant.
    /// </summary>
    /// <param name="resourceType">The type of the resource.</param>
    /// <param name="sourceId">The identifier of the resource in the source tenant.</param>
    /// <param name="targetId">The identifier of the resource in the target tenant.</param>
    public void MapIdentifier(string resourceType, string sourceId, string targetId)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(targetId);

        Identifiers[(resourceType, sourceId)] = targetId;
    }

    /// <summary>
    /// Gets the identifier a source resource has in the target tenant.
    /// </summary>
    /// <param name="resourceType">The type of the resource.</param>
    /// <param name="sourceId">The identifier of the resource in the source tenant.</param>
    /// <param name="targetId">When this method returns <see langword="true"/>, the identifier in the target tenant.</param>
    /// <returns><see langword="true"/> when the identifier is mapped; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTargetIdentifier(
        string resourceType,
        string sourceId,
        [NotNullWhen(true)] out string? targetId
    )
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        ArgumentNullException.ThrowIfNull(sourceId);

        return Identifiers.TryGetValue((resourceType, sourceId), out targetId);
    }
}
