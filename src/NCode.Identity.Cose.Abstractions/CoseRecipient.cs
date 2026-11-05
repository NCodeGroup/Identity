// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes one recipient of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c> message: the key and the key distribution
/// algorithm that yield the content key, and the recipient's header parameters.
/// </summary>
/// <param name="credentials">The recipient key and its key distribution algorithm.</param>
[PublicAPI]
public sealed class CoseRecipient(CoseCredentials credentials)
{
    /// <summary>
    /// Gets the recipient key and its key distribution algorithm.
    /// </summary>
    public CoseCredentials Credentials { get; } =
        credentials ?? throw new ArgumentNullException(nameof(credentials));

    /// <summary>
    /// Gets the extra parameters of the unprotected header bucket of the recipient. The library owns <c>alg</c>. The
    /// protected bucket of a recipient is always empty.
    /// </summary>
    public IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders { get; init; } =
        FrozenDictionary<CoseLabel, CoseValue>.Empty;
}
