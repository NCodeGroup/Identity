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
