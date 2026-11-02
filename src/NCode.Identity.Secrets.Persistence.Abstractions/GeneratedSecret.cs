// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the result of generating a new secret: the data-protected <see cref="PersistedSecret"/> ready to
/// persist, and the one-time unprotected key material for secrets whose value an external party must hold.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GeneratedSecret
{
    /// <summary>
    /// Gets the generated <see cref="PersistedSecret"/> with data-protected key material, ready to be persisted.
    /// </summary>
    public required PersistedSecret Secret { get; init; }

    /// <summary>
    /// Gets the one-time unprotected key material, base64url-encoded, or <c>null</c> when the secret has no
    /// externally-held material to reveal. Populated only for symmetric secrets; asymmetric private keys are
    /// never surfaced. The value exists only in memory and is never persisted.
    /// </summary>
    // See ADR-1050.
    public required string? SecretMaterial { get; init; }
}
