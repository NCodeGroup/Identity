// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;
using NCode.Identity.Secrets.Persistence.Protection;

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
    /// Gets the master key that the change concerns: the new active key of a rotation, or the destroyed key. Its owner
    /// is the owner that the change protects.
    /// </summary>
    // See ADR-1414 and ADR-1429.
    public required MasterKeyInfo Key { get; init; }

    /// <summary>
    /// Gets the master key that a rotation retired, or <c>null</c> when the owner had no active key or the event
    /// records a destruction.
    /// </summary>
    public MasterKeyInfo? RetiredKey { get; init; }

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
            new AuditResource(AuditResourceTypes.MasterKey, Key.KeyId.ToString("D")),
            new AuditResource(Key.Owner.OwnerType, Key.Owner.OwnerId),
        ];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c>, the key's <c>key_id</c>, <c>version</c> and <c>root_key_provider</c>, the
    /// <c>root_key_reference</c> when an owner root key wraps the master key, the <c>root_key_access_identity</c> when
    /// that key is reached through an identity, and for a rotation that retired a key the <c>retired_key_id</c> and
    /// <c>retired_version</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("reason", Reason);
        writer.WriteString("key_id", Key.KeyId);
        writer.WriteNumber("version", Key.Version);
        writer.WriteString("root_key_provider", Key.RootKeyProvider);
        if (Key.RootKeyReference is { } rootKeyReference)
            writer.WriteString("root_key_reference", rootKeyReference);
        if (Key.RootKeyAccessIdentity is { } rootKeyAccessIdentity)
            writer.WriteString("root_key_access_identity", rootKeyAccessIdentity);
        if (RetiredKey is { } retiredKey)
        {
            writer.WriteString("retired_key_id", retiredKey.KeyId);
            writer.WriteNumber("retired_version", retiredKey.Version);
        }
    }
}
