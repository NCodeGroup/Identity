// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Servers;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedServerSettings"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ServerSettingsResource : ISupportServerId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportServerId.ServerId"/>
    public required string ServerId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedServerResource{TValue}.Value"/>
    public required JsonElement Settings { get; init; }
}
