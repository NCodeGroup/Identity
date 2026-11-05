// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the REST resource that lists the master keys of a tenant or of the platform, newest version first.
/// </summary>
// See ADR-1407.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionKeysResource
{
    /// <summary>
    /// Gets the keys, newest version first.
    /// </summary>
    public required List<EncryptionKeyResource> Keys { get; init; }

    /// <summary>
    /// Gets a value indicating whether new keys are also wrapped under a recovery key.
    /// </summary>
    public required bool RecoveryConfigured { get; init; }
}
