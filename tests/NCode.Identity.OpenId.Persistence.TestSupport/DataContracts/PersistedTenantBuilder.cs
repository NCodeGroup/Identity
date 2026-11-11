// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Json;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Builds a <see cref="PersistedTenant"/> for tests. Each property holds a value a test may care about and defaults to
/// the most common fixture value; <see cref="Build"/> copies the tenant identifier into the nested settings and secrets
/// documents, whose concurrency tokens come from <see cref="SettingsConcurrencyToken"/> and
/// <see cref="SecretsConcurrencyToken"/>. <see cref="BuildSettings"/> and <see cref="BuildSecrets"/> build those
/// documents alone.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PersistedTenantBuilder
{
    /// <summary>
    /// Gets the tenant's identifier. Defaults to <c>tenant-1</c>.
    /// </summary>
    public string TenantId { get; init; } = "tenant-1";

    /// <summary>
    /// Gets the identifier of the team the tenant belongs to. Defaults to <c>platform</c>.
    /// </summary>
    public string TeamId { get; init; } = "platform";

    /// <summary>
    /// Gets the issuer identifier of the tenant. Defaults to <see langword="null"/>.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets the mutual-TLS endpoint-alias authority of the tenant. Defaults to <see langword="null"/>.
    /// </summary>
    public string? MtlsEndpointAliasAuthority { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tenant is disabled. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the display name of the tenant. Defaults to <see langword="null"/>, which builds as <see cref="TenantId"/>.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the concurrency token of the tenant. Defaults to an empty string.
    /// </summary>
    public string ConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the tenant's settings document. Defaults to an empty string.
    /// </summary>
    public string SettingsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concurrency token of the tenant's secrets document. Defaults to an empty string.
    /// </summary>
    public string SecretsConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value of the tenant's settings document. Defaults to an empty JSON object.
    /// </summary>
    public JsonElement Settings { get; init; } = JsonElements.EmptyObject;

    /// <summary>
    /// Gets the secrets of the tenant. Defaults to an empty collection.
    /// </summary>
    public IReadOnlyCollection<PersistedSecret> Secrets { get; init; } = [];

    /// <summary>
    /// Builds the tenant's <see cref="PersistedTenantSettings"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedTenantSettings"/> that carries the tenant identifier,
    /// <see cref="SettingsConcurrencyToken"/> and <see cref="Settings"/>.
    /// </returns>
    public PersistedTenantSettings BuildSettings() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = SettingsConcurrencyToken,
            Value = Settings,
        };

    /// <summary>
    /// Builds the tenant's <see cref="PersistedTenantSecrets"/> alone.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedTenantSecrets"/> that carries the tenant identifier,
    /// <see cref="SecretsConcurrencyToken"/> and a copy of <see cref="Secrets"/>.
    /// </returns>
    public PersistedTenantSecrets BuildSecrets() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = SecretsConcurrencyToken,
            Value = [.. Secrets],
        };

    /// <summary>
    /// Builds the <see cref="PersistedTenant"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="PersistedTenant"/> whose settings and secrets come from <see cref="BuildSettings"/> and
    /// <see cref="BuildSecrets"/>.
    /// </returns>
    public PersistedTenant Build() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = ConcurrencyToken,
            TeamId = TeamId,
            Issuer = Issuer,
            MtlsEndpointAliasAuthority = MtlsEndpointAliasAuthority,
            IsDisabled = IsDisabled,
            DisplayName = DisplayName ?? TenantId,
            Settings = BuildSettings(),
            Secrets = BuildSecrets(),
        };
}
