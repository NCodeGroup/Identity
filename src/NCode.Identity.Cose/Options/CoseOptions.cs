// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Options;

/// <summary>
/// The limits of the CBOR Object Signing and Encryption (COSE) services, validated when the host starts. The limits
/// bound the work a received message, map or key can demand.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseOptions
{
    /// <summary>
    /// Gets or sets the largest message, map or key, in bytes, that the library reads; a larger input is refused before
    /// it is parsed. Detached content is the caller's and is not limited. The default value is 1 048 576.
    /// </summary>
    public int MaxMessageSizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the deepest nesting of arrays, maps and tags that a header value, a decoded map or a COSE_Key may
    /// reach. The fixed shape of a COSE message does not count. The default value is 32.
    /// </summary>
    public int MaxNestingDepth { get; set; } = 32;

    /// <summary>
    /// Gets or sets the largest number of signatures that a COSE_Sign message may carry. The default value is 16.
    /// </summary>
    public int MaxSignatures { get; set; } = 16;

    /// <summary>
    /// Gets or sets the largest number of recipients that a COSE_Mac or COSE_Encrypt message may carry. The default
    /// value is 16.
    /// </summary>
    public int MaxRecipients { get; set; } = 16;
}
