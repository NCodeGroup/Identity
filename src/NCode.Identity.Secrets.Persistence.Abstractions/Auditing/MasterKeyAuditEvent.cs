// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Records a change to a master key: the rotation of an owner's key to a new version, or the destruction of a retired
/// key. The event names the key, its owner and its version, and never carries key material, wrapped keys or key
/// checks.
/// </summary>
// See ADR-1407 and ADR-1414.
[PublicAPI]
public sealed record MasterKeyAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <summary>
    /// Gets the change: one of the values in <see cref="MasterKeyChanges"/>.
    /// </summary>
    public required string Change { get; init; }

    /// <summary>
    /// Gets why the change was made: one of the values in <see cref="MasterKeyChangeReasons"/>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the kind of owner of the master key, such as <c>tenant</c> or <c>platform</c>.
    /// </summary>
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner of the master key within its kind.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the identifier of the master key: the new active key of a rotation, or the destroyed key.
    /// </summary>
    public required Guid KeyId { get; init; }

    /// <summary>
    /// Gets the version of the master key.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Gets the name of the root key provider that wraps the master key.
    /// </summary>
    public required string RootKeyProvider { get; init; }

    /// <summary>
    /// Gets the reference of the owner root key that wraps the master key, or <c>null</c> when the deployment's root key
    /// wraps it.
    /// </summary>
    // See ADR-1414.
    public string? RootKeyReference { get; init; }

    /// <summary>
    /// Gets the identifier of the master key that a rotation retired, or <c>null</c> when the owner had no active
    /// key or the event records a destruction.
    /// </summary>
    public Guid? RetiredKeyId { get; init; }

    /// <summary>
    /// Gets the version of the master key that a rotation retired, or <c>null</c> when <see cref="RetiredKeyId"/> is.
    /// </summary>
    public int? RetiredVersion { get; init; }

    /// <inheritdoc />
    public override string EventName => MasterKeyAuditEventNames.For(Change);

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Key;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the master key first and the owner that it protects second.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        [
            new AuditResource(AuditResourceTypes.MasterKey, KeyId.ToString("D")),
            new AuditResource(OwnerType, OwnerId),
        ];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c>, the key's <c>key_id</c>, <c>version</c> and <c>root_key_provider</c>, the
    /// <c>root_key_reference</c> when an owner root key wraps the master key, and for a
    /// rotation that retired a key the <c>retired_key_id</c> and <c>retired_version</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("reason", Reason);
        writer.WriteString("key_id", KeyId);
        writer.WriteNumber("version", Version);
        writer.WriteString("root_key_provider", RootKeyProvider);
        if (RootKeyReference is { } rootKeyReference)
            writer.WriteString("root_key_reference", rootKeyReference);
        if (RetiredKeyId is { } retiredKeyId)
            writer.WriteString("retired_key_id", retiredKeyId);

        if (RetiredVersion is { } retiredVersion)
            writer.WriteNumber("retired_version", retiredVersion);
    }
}
