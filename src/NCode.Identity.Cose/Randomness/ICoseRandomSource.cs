// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Randomness;

/// <summary>
/// Supplies the random bytes that COSE encryption needs: content keys and initialization vectors. The source exists so
/// that a test can reproduce a published test vector.
/// </summary>
internal interface ICoseRandomSource
{
    /// <summary>
    /// Fills the destination with cryptographically secure random bytes.
    /// </summary>
    /// <param name="destination">The bytes to fill.</param>
    void Fill(Span<byte> destination);
}
