// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.Tests.DPoP;

/// <summary>
/// Provides the optional inputs of a DPoP proof that a test creates.
/// </summary>
public sealed class DPoPProofTestOptions
{
    /// <summary>
    /// Gets the HTTP URI of the proof, or <see langword="null"/> for the default URI of the tests.
    /// </summary>
    public string? HttpUri { get; init; }

    /// <summary>
    /// Gets the access token that the proof is bound to, or <see langword="null"/> for none.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the server nonce of the proof, or <see langword="null"/> for none.
    /// </summary>
    public string? Nonce { get; init; }
}
