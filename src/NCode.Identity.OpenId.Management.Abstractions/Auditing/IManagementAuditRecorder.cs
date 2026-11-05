// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Events.Audit;
using NCode.Identity.Secrets.Persistence.Protection;

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
    /// Records that an administrator bulk-revoked grants by subject, client, and/or session filter.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the grants belong to.</param>
    /// <param name="subjectId">The subject filter applied to the revocation, if any.</param>
    /// <param name="clientId">The client filter applied to the revocation, if any.</param>
    /// <param name="sessionId">The session (<c>sid</c>) filter applied to the revocation, if any.</param>
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
        string? sessionId,
        long revokedCount,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator revoked end-user sessions — a single session by its <c>sid</c> or every session a
    /// subject holds. The per-session termination side effects (grant revocation, back-channel logout, and the
    /// <c>session.terminated</c> audit event) are raised independently by the session-termination seam.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant the sessions belong to.</param>
    /// <param name="subjectId">The subject whose sessions were revoked, if known.</param>
    /// <param name="sessionId">The opaque <c>sid</c> of the single session revoked, or <c>null</c> for a subject-wide revocation.</param>
    /// <param name="revokedCount">The number of sessions that were revoked.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordSessionsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? sessionId,
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
    /// Records that an administrator created, updated, or deleted a team, or updated its settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the affected team.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the team's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordTeamChangedAsync(
        HttpContext httpContext,
        string teamId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an administrator created, updated, or deleted an OpenID tenant, or updated its settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team the affected tenant belongs to.</param>
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
        string teamId,
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
    /// Records that an administrator updated the platform's settings. The event carries the tenant the request runs
    /// under and the platform's identifier, <c>platform</c>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="changeType">
    /// The lifecycle transition that occurred; one of the <see cref="ResourceChangeTypes"/> values.
    /// </param>
    /// <param name="resourceValues">
    /// A non-sensitive snapshot of the resource's values, or <c>null</c> when none is captured.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordPlatformChangedAsync(
        HttpContext httpContext,
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
    /// Records an arbitrary resource-change audit event, deriving the common audit envelope — the event id, event time,
    /// acting administrator, request context, and event source — from the current request and publishing through the
    /// event seam. This is the extension seam a capability package (for example, local accounts) uses to record its own
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

    /// <summary>
    /// Records that an administrator rekeyed the master key of a tenant or of the server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="rotation">The rotation that the rekey performed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    // See ADR-1407.
    ValueTask RecordMasterKeyRotatedAsync(
        HttpContext httpContext,
        MasterKeyRotation rotation,
        CancellationToken cancellationToken
    );
}
