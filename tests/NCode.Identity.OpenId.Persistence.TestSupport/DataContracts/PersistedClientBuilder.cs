// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Json;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Builds a <see cref="PersistedClient"/> for tests. Each property holds a value a test may care about and defaults to
/// the most common fixture value; <see cref="Build"/> copies the tenant and client identifiers into the nested settings
/// and secrets documents, whose concurrency tokens come from <see cref="SettingsConcurrencyToken"/> and
/// <see cref="SecretsConcurrencyToken"/>. <see cref="BuildSettings"/> and <see cref="BuildSecrets"/> build those
/// documents alone.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PersistedClientBuilder
{
    /// <summary>
    /// Gets the client's tenant identifier. Defaults to <c>tenant-1</c>.
    /// </summary>
    public string TenantId { get; init; } = "tenant-1";

    /// <summary>
    /// Gets the client's identifier. Defaults to <c>client-1</c>.
    /// </summary>
    public string ClientId { get; init; } = "client-1";

    /// <summary>
    /// Gets the identifier of the client's principal. Defaults to an empty string.
    /// </summary>
    public string PrincipalId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the client. Defaults to an empty string.
    /// </summary>
    public string ConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the client's settings document. Defaults to an empty string.
    /// </summary>
    public string SettingsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the client's secrets document. Defaults to an empty string.
    /// </summary>
    public string SecretsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the client is disabled. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the type of the node that owns the client. Defaults to <see langword="null"/>.
    /// </summary>
    public string? OwningNodeType { get; init; }

    /// <summary>
    /// Gets the identifier of the node that owns the client. Defaults to <see langword="null"/>.
    /// </summary>
    public string? OwningNodeId { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client belongs to. Defaults to <see langword="null"/>.
    /// </summary>
    public string? ResourceServerId { get; init; }

    /// <summary>
    /// Gets the value of the client's settings document. Defaults to an empty JSON object.
    /// </summary>
    public JsonElement Settings { get; init; } = JsonElements.EmptyObject;

    /// <summary>
    /// Gets the secrets of the client. Defaults to an empty collection.
    /// </summary>
    public IReadOnlyCollection<PersistedSecret> Secrets { get; init; } = [];

    /// <summary>
    /// Builds the client's <see cref="PersistedClientSettings"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedClientSettings"/> that carries the tenant and client identifiers,
    /// <see cref="SettingsConcurrencyToken"/> and <see cref="Settings"/>.
    /// </returns>
    public PersistedClientSettings BuildSettings() =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = SettingsConcurrencyToken,
            Value = Settings,
        };

    /// <summary>
    /// Builds the client's <see cref="PersistedClientSecrets"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedClientSecrets"/> that carries the tenant and client identifiers,
    /// <see cref="SecretsConcurrencyToken"/> and a copy of <see cref="Secrets"/>.
    /// </returns>
    public PersistedClientSecrets BuildSecrets() =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = SecretsConcurrencyToken,
            Value = [.. Secrets],
        };

    /// <summary>
    /// Builds the <see cref="PersistedClient"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedClient"/> whose settings and secrets come from <see cref="BuildSettings"/> and
    /// <see cref="BuildSecrets"/>.
    /// </returns>
    public PersistedClient Build() =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = ConcurrencyToken,
            IsDisabled = IsDisabled,
            PrincipalId = PrincipalId,
            ResourceServerId = ResourceServerId,
            OwningNodeType = OwningNodeType,
            OwningNodeId = OwningNodeId,
            Settings = BuildSettings(),
            Secrets = BuildSecrets(),
        };
}
