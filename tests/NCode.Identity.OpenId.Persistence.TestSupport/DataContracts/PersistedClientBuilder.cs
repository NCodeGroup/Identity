// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Json;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Builds a <see cref="PersistedClient"/> for tests. Each property holds a value a test may care about and defaults to
/// the most common fixture value; <see cref="Build"/> copies the tenant and client identifiers and the concurrency
/// tokens into the nested settings and secrets documents.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PersistedClientBuilder
{
    public string TenantId { get; init; } = "tenant-1";

    public string ClientId { get; init; } = "client-1";

    public string PrincipalId { get; init; } = string.Empty;

    public string ConcurrencyToken { get; init; } = string.Empty;

    public string SettingsConcurrencyToken { get; init; } = string.Empty;

    public string SecretsConcurrencyToken { get; init; } = string.Empty;

    public bool IsDisabled { get; init; }

    public string? OwningNodeType { get; init; }

    public string? OwningNodeId { get; init; }

    public string? ResourceServerId { get; init; }

    public JsonElement Settings { get; init; } = JsonElements.EmptyObject;

    public IReadOnlyCollection<PersistedSecret> Secrets { get; init; } = [];

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
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = SettingsConcurrencyToken,
                Value = Settings,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = SecretsConcurrencyToken,
                Value = [.. Secrets],
            },
        };
}
