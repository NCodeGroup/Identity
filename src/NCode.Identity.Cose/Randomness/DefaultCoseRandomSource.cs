// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Cose.Randomness;

/// <summary>
/// Provides the default <see cref="ICoseRandomSource"/> over the platform's cryptographically secure random number
/// generator.
/// </summary>
internal class DefaultCoseRandomSource : ICoseRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
