// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Records a change to a remote key: the destruction of a key that a key management service holds. The event names the
/// key, its group and the owner and secret that created it, and never carries key material.
/// </summary>
// See ADR-1478.
[PublicAPI]
public sealed record RemoteKeyAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <summary>
    /// Gets the change: one of the values in <see cref="RemoteKeyChanges"/>.
    /// </summary>
    public required string Change { get; init; }

    /// <summary>
    /// Gets why the change was made: one of the values in <see cref="RemoteKeyChangeReasons"/>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the name of the remote key provider that holds the key.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the identifier of the key in the key management service.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the name of the group that the key was created for.
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Gets the identifier of the owner that the key was created for, or <c>null</c> when the service does not record
    /// it.
    /// </summary>
    public string? OwnerId { get; init; }

    /// <summary>
    /// Gets the identifier of the secret that the key was created for, or <c>null</c> when the service does not record
    /// it.
    /// </summary>
    public string? SecretId { get; init; }

    /// <summary>
    /// Gets when the key was created in the key management service.
    /// </summary>
    public required DateTimeOffset KeyCreatedWhen { get; init; }

    /// <inheritdoc />
    public override string EventName => RemoteKeyAuditEventNames.For(Change);

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Key;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the key by its reference, <c>provider:keyId</c>.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        [new AuditResource(AuditResourceTypes.RemoteKey, $"{Provider}:{KeyId}")];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c>, <c>provider</c>, <c>key_id</c> and <c>group</c>, the <c>owner</c> and <c>secret_id</c>
    /// when set, and the <c>key_created_when</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("reason", Reason);
        writer.WriteString("provider", Provider);
        writer.WriteString("key_id", KeyId);
        writer.WriteString("group", GroupName);
        if (OwnerId is { } ownerId)
            writer.WriteString("owner", ownerId);
        if (SecretId is { } secretId)
            writer.WriteString("secret_id", secretId);
        writer.WriteString("key_created_when", KeyCreatedWhen);
    }
}
