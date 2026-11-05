// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes one signer of a <c>COSE_Sign</c> message: its credentials and the header parameters of its signature.
/// </summary>
/// <param name="credentials">The credentials of the signer.</param>
[PublicAPI]
public sealed class CoseSigner(CoseCredentials credentials)
{
    /// <summary>
    /// Gets the credentials of the signer.
    /// </summary>
    public CoseCredentials Credentials { get; } =
        credentials ?? throw new ArgumentNullException(nameof(credentials));

    /// <summary>
    /// Gets the extra parameters of the protected header bucket of the signature. The library owns <c>alg</c>.
    /// </summary>
    public IReadOnlyDictionary<CoseLabel, CoseValue> ProtectedHeaders { get; init; } =
        FrozenDictionary<CoseLabel, CoseValue>.Empty;

    /// <summary>
    /// Gets the parameters of the unprotected header bucket of the signature.
    /// </summary>
    public IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders { get; init; } =
        FrozenDictionary<CoseLabel, CoseValue>.Empty;
}
