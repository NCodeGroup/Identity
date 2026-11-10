// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.SdJwt.Tests.Verification;

/// <summary>
/// The inputs a verifier test varies when it presents an SD-JWT.
/// </summary>
public sealed class SdJwtTestPresentationOptions
{
    /// <summary>
    /// Gets the disclosures to present, or <see langword="null"/> for every disclosure of the SD-JWT.
    /// </summary>
    public IReadOnlyList<Disclosure>? Disclosures { get; init; }

    /// <summary>
    /// Gets a value indicating whether the presentation carries a Key Binding JWT.
    /// </summary>
    public bool WithKeyBinding { get; init; }

    /// <summary>
    /// Gets the nonce of the Key Binding JWT, or <see langword="null"/> for the test's default nonce.
    /// </summary>
    public string? Nonce { get; init; }
}
