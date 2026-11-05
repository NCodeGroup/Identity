// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Holds one signature of a <c>COSE_Sign</c> message, or one recipient of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c>
/// message, as it is written.
/// </summary>
// See ADR-1409.
internal sealed record CoseLayerEntry(
    byte[] ProtectedBytes,
    IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders,
    byte[] Third
);
