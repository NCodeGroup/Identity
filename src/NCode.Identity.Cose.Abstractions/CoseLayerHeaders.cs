// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Holds the header buckets of one layer of a COSE message: the message body, a signature or a recipient.
/// </summary>
[PublicAPI]
public sealed class CoseLayerHeaders
{
    /// <summary>
    /// Gets the protected header bucket, which the layer's signature, authentication code or authenticated encryption
    /// covers.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> ProtectedHeaders { get; init; }

    /// <summary>
    /// Gets the unprotected header bucket, which nothing authenticates.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders { get; init; }
}
