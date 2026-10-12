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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Records audit events for the management API by deriving the common audit envelope — including the
/// acting administrator — from the current <see cref="HttpContext"/> and publishing through the event
/// seam.
/// </summary>
/// <remarks>
/// Management operations have no end-user subject; the <em>actor</em> is the administrator performing
/// the action, derived from the authenticated principal on the request. Audit events carry identifiers
/// and metadata only, never secret material.
/// </remarks>
[PublicAPI]
public interface IManagementAuditRecorder
{
    /// <summary>
    /// Records that an administrator revoked a single grant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the grant belongs to.</param>
    /// <param name="subjectId">The identifier of the subject the grant was issued for, if any.</param>
    /// <param name="clientId">The identifier of the client the grant was issued to, if any.</param>
    /// <param name="grantType">The type of the revoked grant, if known.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordGrantRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        string? grantType,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator bulk-revoked grants by subject and/or client filter.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the grants belong to.</param>
    /// <param name="subjectId">The subject filter applied to the revocation, if any.</param>
    /// <param name="clientId">The client filter applied to the revocation, if any.</param>
    /// <param name="revokedCount">The number of grants that were revoked.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordGrantsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        long revokedCount,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted an OpenID client, or updated its settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the client belongs to.</param>
    /// <param name="clientId">The identifier of the affected client.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the client's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordClientChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted a secret belonging to an OpenID client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the client belongs to.</param>
    /// <param name="clientId">The identifier of the client the secret belongs to.</param>
    /// <param name="secretId">The identifier of the affected secret.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the secret's metadata, or <c>null</c> when none is captured. Never
    /// contains secret material.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordClientSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted an OpenID tenant, or updated its settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the affected tenant.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the tenant's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordTenantChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted a secret belonging to an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the secret belongs to.</param>
    /// <param name="secretId">The identifier of the affected secret.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the secret's metadata, or <c>null</c> when none is captured. Never
    /// contains secret material.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordTenantSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted an OpenID server, or updated its settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the affected server.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the server's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordServerChangedAsync(
        HttpContext httpContext,
        string serverId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted a secret belonging to an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the server the secret belongs to.</param>
    /// <param name="secretId">The identifier of the affected secret.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the secret's metadata, or <c>null</c> when none is captured. Never
    /// contains secret material.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordServerSecretChangedAsync(
        HttpContext httpContext,
        string serverId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted a resource server (API), or updated its settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the resource server belongs to.</param>
    /// <param name="resourceServerId">The identifier of the affected resource server.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the resource server's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordResourceServerChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted a scope belonging to a resource server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the resource server belongs to.</param>
    /// <param name="resourceServerId">The identifier of the resource server the scope belongs to.</param>
    /// <param name="scopeValue">The value of the affected scope.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the scope's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordResourceServerScopeChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string scopeValue,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted a secret belonging to a resource server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the resource server belongs to.</param>
    /// <param name="resourceServerId">The identifier of the resource server the secret belongs to.</param>
    /// <param name="secretId">The identifier of the affected secret.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the secret's metadata, or <c>null</c> when none is captured. Never
    /// contains secret material.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordResourceServerSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records an arbitrary resource-change audit event, deriving the common audit envelope — the event id, timestamp,
    /// correlation id, acting administrator, and source — from the current request and publishing through the event
    /// seam. This is the extension seam a capability package (for example, local accounts) uses to record its own
    /// resource-change events without the base management slice knowing their shape.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="auditEvent">
    /// The resource-change event to stamp and publish. The caller supplies the resource-specific fields (tenant,
    /// resource id, change type, non-sensitive snapshot, and outcome); the envelope fields are stamped by the recorder.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordResourceChangeAsync(
        HttpContext httpContext,
        ResourceChangeAuditEvent auditEvent,
        CancellationToken cancellationToken
    );
}
