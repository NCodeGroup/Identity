// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the REST resource returned by a rekey: the key that became active and the key that it retired.
/// </summary>
// See ADR-1407.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionKeyRotationResource
{
    /// <summary>
    /// Gets the key that is now active.
    /// </summary>
    public required EncryptionKeyResource ActiveKey { get; init; }

    /// <summary>
    /// Gets the key that the rekey retired, or <c>null</c> when the owner had no active key.
    /// </summary>
    public EncryptionKeyResource? RetiredKey { get; init; }
}
