// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Platform;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedPlatform"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PlatformResource : ISupportPlatformId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportPlatformId.PlatformId"/>
    public required string PlatformId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets the display name for the server.
    /// </summary>
    public required string DisplayName { get; init; }
}
