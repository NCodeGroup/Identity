// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using JetBrains.Annotations;
using NCode.Identity.Cose;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cwt.Issuance;

/// <summary>
/// Describes a CBOR Web Token (CWT) to issue: its claims and how it is protected.
/// </summary>
// See ADR-1410.
[PublicAPI]
public sealed class CwtIssuanceParameters
{
    /// <summary>
    /// Gets the credentials that protect the claims set: a signature algorithm yields a <c>COSE_Sign1</c> message and a
    /// message authentication code algorithm a <c>COSE_Mac0</c> message.
    /// </summary>
    public required CoseCredentials Credentials { get; init; }

    /// <summary>
    /// Gets the <c>iss</c> claim.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets the <c>sub</c> claim.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the audiences of the token: one is written as a text string, several as an array, and none as no claim.
    /// </summary>
    public IReadOnlyList<string> Audiences { get; init; } = [];

    /// <summary>
    /// Gets the <c>exp</c> claim.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the <c>nbf</c> claim.
    /// </summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>
    /// Gets the <c>iat</c> claim; the current time when <c>null</c>.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>
    /// Gets the <c>cti</c> claim.
    /// </summary>
    public ReadOnlyMemory<byte>? CwtId { get; init; }

    /// <summary>
    /// Gets the key the token is bound to, written as the public <c>COSE_Key</c> member of the <c>cnf</c> claim.
    /// </summary>
    public SecretKey? ConfirmationKey { get; init; }

    /// <summary>
    /// Gets the type of the token, written as the protected <c>typ</c> header parameter (RFC 9596).
    /// </summary>
    public string? TokenType { get; init; }

    /// <summary>
    /// Gets further claims. The claims the typed members own, keys one through eight, are refused.
    /// </summary>
    public IReadOnlyDictionary<CoseLabel, CoseValue> AdditionalClaims { get; init; } =
        FrozenDictionary<CoseLabel, CoseValue>.Empty;

    /// <summary>
    /// Gets the credentials that encrypt the token as a <c>COSE_Encrypt0</c> message.
    /// </summary>
    public CoseCredentials? EncryptionCredentials { get; init; }

    /// <summary>
    /// Gets the recipients that encrypt the token as a <c>COSE_Encrypt</c> message.
    /// </summary>
    public CoseRecipients? EncryptionRecipients { get; init; }

    /// <summary>
    /// Gets a value indicating whether the CWT tag is written before the token.
    /// </summary>
    public bool WriteCwtTag { get; init; }
}
