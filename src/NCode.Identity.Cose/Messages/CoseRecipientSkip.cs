// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Why a recipient is not eligible for the caller.
/// </summary>
// See ADR-1409.
internal enum CoseRecipientSkip
{
    /// <summary>
    /// The recipient is eligible.
    /// </summary>
    None,

    /// <summary>
    /// The caller does not permit the recipient's key distribution algorithm.
    /// </summary>
    NotPermitted,

    /// <summary>
    /// The recipient's key distribution algorithm is not supported.
    /// </summary>
    Unsupported,
}
