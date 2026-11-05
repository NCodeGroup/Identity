// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Cose;

namespace NCode.Identity.Cwt;

/// <summary>
/// The claims set of a CBOR Web Token (CWT) with the registered claims read into typed members. Every claim, typed or
/// not, is also available in <see cref="Claims"/> as received.
/// </summary>
// See ADR-1410.
[PublicAPI]
public sealed class CwtClaimsSet
{
    /// <summary>
    /// Gets every claim as received.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> Claims { get; init; }

    /// <summary>
    /// Gets the <c>iss</c> claim, or <c>null</c> when absent.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets the <c>sub</c> claim, or <c>null</c> when absent.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the values of the <c>aud</c> claim, and none when it is absent.
    /// </summary>
    public IReadOnlyList<string> Audiences { get; init; } = [];

    /// <summary>
    /// Gets the <c>exp</c> claim, or <c>null</c> when absent.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the <c>nbf</c> claim, or <c>null</c> when absent.
    /// </summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>
    /// Gets the <c>iat</c> claim, or <c>null</c> when absent.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>
    /// Gets the <c>cti</c> claim, or <c>null</c> when absent.
    /// </summary>
    public ReadOnlyMemory<byte>? CwtId { get; init; }

    /// <summary>
    /// Gets the encoded <c>COSE_Key</c> member of the <c>cnf</c> claim, or <c>null</c> when absent.
    /// </summary>
    public ReadOnlyMemory<byte>? ConfirmationKey { get; init; }
}
