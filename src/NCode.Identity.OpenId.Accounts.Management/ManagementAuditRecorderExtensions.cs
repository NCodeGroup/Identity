// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Accounts.Management.Auditing;
using NCode.Identity.OpenId.Management.Auditing;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Records the local-account management audit event through the generic management audit seam; the local-account audit
/// shape is owned by the accounts capability.
/// </summary>
internal static class ManagementAuditRecorderExtensions
{
    /// <summary>
    /// Records that an administrator created, updated, enabled, disabled, reset the credential of, changed the metadata
    /// of, or deleted a local account.
    /// </summary>
    public static ValueTask RecordLocalAccountChangedAsync(
        this IManagementAuditRecorder recorder,
        HttpContext httpContext,
        string tenantId,
        string localAccountId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    ) =>
        recorder.RecordResourceChangeAsync(
            httpContext,
            new LocalAccountChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = tenantId,
                ResourceId = localAccountId,
                ChangeType = changeType,
                ResourceValues = resourceValues,
                Outcome = AuditOutcome.Success,
            },
            cancellationToken
        );
}
