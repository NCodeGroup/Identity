// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Servers;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedServerSecrets"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ServerSecretsResource : ISupportServerId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportServerId.ServerId"/>
    public required string ServerId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets the collection of secrets only known to an OpenID Server instance.
    /// </summary>
    public required IReadOnlyCollection<SecretResource> Secrets { get; init; }
}
