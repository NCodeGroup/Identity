// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Json;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Builds a <see cref="PersistedResourceServer"/> for tests. Each property holds a value a test may care about and
/// defaults to the most common fixture value; <see cref="Build"/> copies the tenant and resource server identifiers into
/// the nested settings and secrets documents, whose concurrency tokens come from
/// <see cref="SettingsConcurrencyToken"/> and <see cref="SecretsConcurrencyToken"/>. <see cref="BuildSettings"/> and
/// <see cref="BuildSecrets"/> build those documents alone.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PersistedResourceServerBuilder
{
    /// <summary>
    /// Gets the identifier of the tenant that owns the resource server. Defaults to <c>tenant-1</c>.
    /// </summary>
    public string TenantId { get; init; } = "tenant-1";

    /// <summary>
    /// Gets the resource server's identifier. Defaults to <c>resource-server-1</c>.
    /// </summary>
    public string ResourceServerId { get; init; } = "resource-server-1";

    /// <summary>
    /// Gets the audience identifier of the resource server. Defaults to <c>https://api.example.com</c>.
    /// </summary>
    public string Identifier { get; init; } = "https://api.example.com";

    /// <summary>
    /// Gets the human-readable name of the resource server. Defaults to <c>Example API</c>.
    /// </summary>
    public string Name { get; init; } = "Example API";

    /// <summary>
    /// Gets a value indicating whether the resource server is system-owned. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// Gets a value indicating whether the resource server is disabled. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the concurrency token of the resource server. Defaults to an empty string.
    /// </summary>
    public string ConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the resource server's settings document. Defaults to an empty string.
    /// </summary>
    public string SettingsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the resource server's secrets document. Defaults to an empty string.
    /// </summary>
    public string SecretsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value of the resource server's settings document. Defaults to an empty JSON object.
    /// </summary>
    public JsonElement Settings { get; init; } = JsonElements.EmptyObject;

    /// <summary>
    /// Gets the scopes of the resource server. Defaults to an empty collection.
    /// </summary>
    public IReadOnlyCollection<PersistedScope> Scopes { get; init; } = [];

    /// <summary>
    /// Gets the secrets of the resource server. Defaults to an empty collection.
    /// </summary>
    public IReadOnlyCollection<PersistedSecret> Secrets { get; init; } = [];

    /// <summary>
    /// Builds the resource server's <see cref="PersistedResourceServerSettings"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedResourceServerSettings"/> that carries the tenant and resource server identifiers,
    /// <see cref="SettingsConcurrencyToken"/> and <see cref="Settings"/>.
    /// </returns>
    public PersistedResourceServerSettings BuildSettings() =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = SettingsConcurrencyToken,
            Value = Settings,
        };

    /// <summary>
    /// Builds the resource server's <see cref="PersistedResourceServerSecrets"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedResourceServerSecrets"/> that carries the tenant and resource server identifiers,
    /// <see cref="SecretsConcurrencyToken"/> and a copy of <see cref="Secrets"/>.
    /// </returns>
    public PersistedResourceServerSecrets BuildSecrets() =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = SecretsConcurrencyToken,
            Value = [.. Secrets],
        };

    /// <summary>
    /// Builds the <see cref="PersistedResourceServer"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedResourceServer"/> whose settings and secrets come from <see cref="BuildSettings"/> and
    /// <see cref="BuildSecrets"/>.
    /// </returns>
    public PersistedResourceServer Build() =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = ConcurrencyToken,
            Identifier = Identifier,
            Name = Name,
            IsSystem = IsSystem,
            IsDisabled = IsDisabled,
            Settings = BuildSettings(),
            Scopes = [.. Scopes],
            Secrets = BuildSecrets(),
        };
}
