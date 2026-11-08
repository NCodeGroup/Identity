// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Records a change the server made to one of a tenant's keys on its own: it generated, activated, retired or removed
/// the key, in a seed or in the background rollover pass. The event names the key and the instants of its lifecycle
/// and never carries key material.
/// </summary>
// See ADR-1404.
[PublicAPI]
public sealed record TenantKeyAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <summary>
    /// Gets the change: one of the values in <see cref="TenantKeyChanges"/>.
    /// </summary>
    public required string Change { get; init; }

    /// <summary>
    /// Gets why the server made the change: one of the values in <see cref="TenantKeyChangeReasons"/>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the identifier of the key's secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the key identifier (<c>kid</c>) the key is published under.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the type of the key's secret.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the algorithm the key is bound to, or <c>null</c> when it is bound to none.
    /// </summary>
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the key's use, or <c>null</c> when it has none.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the name of the remote key provider that holds the key, or <c>null</c> when the key is held in the
    /// server's store.
    /// </summary>
    public string? RemoteKeyProvider { get; init; }

    /// <summary>
    /// Gets the instant the key activates.
    /// </summary>
    public required DateTimeOffset ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the instant the key retires.
    /// </summary>
    public required DateTimeOffset RetiresWhen { get; init; }

    /// <summary>
    /// Gets the instant the key expires.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <inheritdoc />
    public override string EventName => TenantKeyAuditEventNames.For(Change);

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Key;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the key's secret first and the tenant that owns it second.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        TenantId is null
            ? [new AuditResource(AuditResourceTypes.TenantSecret, SecretId)]
            :
            [
                new AuditResource(AuditResourceTypes.TenantSecret, SecretId),
                new AuditResource(AuditResourceTypes.Tenant, TenantId),
            ];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c>, the key's <c>key_id</c>, <c>secret_type</c>, <c>algorithm</c> and <c>use</c>, the
    /// <c>remote_key_provider</c> of a key a key management service holds, and the instants <c>activates_when</c>,
    /// <c>retires_when</c> and <c>expires_when</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("reason", Reason);
        writer.WriteString("key_id", KeyId);
        writer.WriteString("secret_type", SecretType);
        if (Algorithm is not null)
        {
            writer.WriteString("algorithm", Algorithm);
        }

        if (Use is not null)
        {
            writer.WriteString("use", Use);
        }

        if (RemoteKeyProvider is not null)
        {
            writer.WriteString("remote_key_provider", RemoteKeyProvider);
        }

        writer.WriteString("activates_when", ActivatesWhen);
        writer.WriteString("retires_when", RetiresWhen);
        writer.WriteString("expires_when", ExpiresWhen);
    }
}
