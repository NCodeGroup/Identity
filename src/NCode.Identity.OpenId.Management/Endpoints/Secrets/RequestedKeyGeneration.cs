// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Describes the key a create request asks for: its size and curve, and the use and algorithm it declares.
/// </summary>
internal readonly record struct RequestedKeyGeneration
{
    /// <summary>
    /// Gets the size, in bits, of the requested key.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the requested key's curve, or <c>null</c> when the request names none.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the use (<c>sig</c> or <c>enc</c>) declared for the requested key, or <c>null</c> when none is declared.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the code of the algorithm declared for the requested key, or <c>null</c> when none is declared.
    /// </summary>
    public string? Algorithm { get; init; }
}
