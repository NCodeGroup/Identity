// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using JetBrains.Annotations;
using NCode.Identity.Cose.Algorithms;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes the recipients of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c> message and the algorithm that protects its
/// content.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseRecipients
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoseRecipients"/> class.
    /// </summary>
    /// <param name="contentAlgorithm">The message authentication code or content encryption algorithm.</param>
    /// <param name="recipients">The recipients; at least one.</param>
    /// <exception cref="ArgumentException"><paramref name="recipients"/> is empty.</exception>
    public CoseRecipients(CoseAlgorithm contentAlgorithm, IReadOnlyList<CoseRecipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(contentAlgorithm);
        ArgumentNullException.ThrowIfNull(recipients);

        if (recipients.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(recipients));

        ContentAlgorithm = contentAlgorithm;
        Recipients = [.. recipients];
    }

    /// <summary>
    /// Gets the message authentication code or content encryption algorithm.
    /// </summary>
    public CoseAlgorithm ContentAlgorithm { get; }

    /// <summary>
    /// Gets the recipients.
    /// </summary>
    public ImmutableArray<CoseRecipient> Recipients { get; }
}
