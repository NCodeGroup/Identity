// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes how a COSE message is encoded: the header parameters of its body, the external data it is bound to, and
/// whether its content is detached or its tag omitted.
/// </summary>
[PublicAPI]
public sealed class CoseEncodingOptions
{
    /// <summary>
    /// Gets the extra parameters of the protected header bucket of the message body. The library owns <c>alg</c>,
    /// <c>IV</c> and <c>Partial IV</c>.
    /// </summary>
    public IReadOnlyDictionary<CoseLabel, CoseValue> ProtectedHeaders { get; init; } =
        FrozenDictionary<CoseLabel, CoseValue>.Empty;

    /// <summary>
    /// Gets the parameters of the unprotected header bucket of the message body.
    /// </summary>
    public IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders { get; init; } =
        FrozenDictionary<CoseLabel, CoseValue>.Empty;

    /// <summary>
    /// Gets the external additional authenticated data that the signature, authentication code or encryption is bound
    /// to without carrying it in the message.
    /// </summary>
    public ReadOnlyMemory<byte> ExternalAad { get; init; }

    /// <summary>
    /// Gets a value indicating whether the content, or the ciphertext, is left out of the message. The verifier then
    /// supplies it.
    /// </summary>
    public bool DetachContent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the COSE tag is left out of the message. The verifier then names the message
    /// type.
    /// </summary>
    public bool OmitTag { get; init; }
}
