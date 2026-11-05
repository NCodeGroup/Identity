// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes what a verifier accepts of a signed or authenticated COSE message: the algorithms it permits, the keys it
/// trusts and the external data the message is bound to. Nothing is inferred from the message.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseVerificationParameters
{
    /// <summary>
    /// Gets the identifiers of the signature or message authentication code algorithms the verifier permits. The list
    /// must not be empty.
    /// </summary>
    public required IReadOnlyCollection<int> ValidAlgorithms { get; init; }

    /// <summary>
    /// Gets the keys the verifier trusts for the message.
    /// </summary>
    public required IReadOnlyCollection<SecretKey> Keys { get; init; }

    /// <summary>
    /// Gets the identifiers of the key distribution algorithms the verifier permits for the recipients of a
    /// <c>COSE_Mac</c> message.
    /// </summary>
    public IReadOnlyCollection<int> ValidKeyDistributionAlgorithms { get; init; } = [];

    /// <summary>
    /// Gets the external additional authenticated data the message is bound to.
    /// </summary>
    public ReadOnlyMemory<byte> ExternalAad { get; init; }

    /// <summary>
    /// Gets the detached content, or <c>null</c> when the message carries its content.
    /// </summary>
    public ReadOnlyMemory<byte>? DetachedContent { get; init; }

    /// <summary>
    /// Gets the labels of critical header parameters the verifier understands beyond the ones the library does.
    /// </summary>
    public IReadOnlyCollection<CoseLabel> UnderstoodCriticalHeaders { get; init; } = [];

    /// <summary>
    /// Gets the type of the message when it carries no tag, or <c>null</c> to refuse an untagged message.
    /// </summary>
    public CoseMessageType? UntaggedMessageType { get; init; }

    /// <summary>
    /// Gets the tolerance for the expiry of a key. The default value is five minutes.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);
}
