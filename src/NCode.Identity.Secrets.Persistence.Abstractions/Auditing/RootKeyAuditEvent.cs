// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Records a change to the root key that an owner brings: setting it, starting to remove it, its becoming unavailable
/// and its being restored. The event names the key's wrapper and reference and the owner, and never carries key
/// material.
/// </summary>
// See ADR-1411.
[PublicAPI]
public sealed record RootKeyAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <summary>
    /// Gets the change: one of the values in <see cref="RootKeyChanges"/>.
    /// </summary>
    public required string Change { get; init; }

    /// <summary>
    /// Gets why the change is made: one of the values in <see cref="RootKeyChangeReasons"/>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the kind of owner of the root key, such as <c>tenant</c>.
    /// </summary>
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner of the root key within its kind.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the name of the key wrapper that wraps under the root key.
    /// </summary>
    public required string KeyWrapperName { get; init; }

    /// <summary>
    /// Gets the reference to the root key that the owner gave.
    /// </summary>
    public required string KeyReference { get; init; }

    /// <summary>
    /// Gets the state of the root key after the change: one of the values in
    /// <see cref="Protection.OwnerRootKeyStates"/>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the description of the failure that made the key unavailable, or <c>null</c> when the change is not a
    /// failure.
    /// </summary>
    public string? Failure { get; init; }

    /// <inheritdoc />
    public override string EventName => RootKeyAuditEventNames.For(Change);

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Key;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the root key first and the owner whose master keys it wraps second.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        [
            new AuditResource(AuditResourceTypes.RootKey, KeyReference),
            new AuditResource(OwnerType, OwnerId),
        ];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c>, the <c>provider</c>, the <c>key_reference</c> and the <c>state</c>, and the
    /// <c>failure</c> when there is one.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("reason", Reason);
        writer.WriteString("provider", KeyWrapperName);
        writer.WriteString("key_reference", KeyReference);
        writer.WriteString("state", State);
        if (Failure is { } failure)
            writer.WriteString("failure", failure);
    }
}
