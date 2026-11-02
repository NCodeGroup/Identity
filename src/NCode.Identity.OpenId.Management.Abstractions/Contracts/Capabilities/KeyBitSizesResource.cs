// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes an inclusive range of legal key sizes, in bits, mirroring <see cref="System.Security.Cryptography.KeySizes"/>.
/// A <see cref="SkipBits"/> of <c>0</c> denotes a single fixed size equal to <see cref="MinBits"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class KeyBitSizesResource
{
    /// <summary>
    /// Gets the smallest legal key size, in bits.
    /// </summary>
    public required int MinBits { get; init; }

    /// <summary>
    /// Gets the largest legal key size, in bits.
    /// </summary>
    public required int MaxBits { get; init; }

    /// <summary>
    /// Gets the increment, in bits, between legal key sizes. A value of <c>0</c> denotes a single fixed size.
    /// </summary>
    public required int SkipBits { get; init; }
}
