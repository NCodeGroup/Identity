// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Checks the syntax RFC 7636 gives the PKCE <c>code_verifier</c> (§4.1) and <c>code_challenge</c> (§4.2): 43 to 128
/// characters from the unreserved set — letters, digits, <c>-</c>, <c>.</c>, <c>_</c>, and <c>~</c>.
/// </summary>
internal static class PkceValueSyntax
{
    private const int MinLength = 43;
    private const int MaxLength = 128;

    private static SearchValues<char> Unreserved { get; } =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~");

    /// <summary>
    /// Gets a value indicating whether <paramref name="value"/> is a well-formed <c>code_verifier</c> or
    /// <c>code_challenge</c>.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value has the RFC 7636 syntax.</returns>
    public static bool IsWellFormed(string value) =>
        value.Length is >= MinLength and <= MaxLength
        && !value.AsSpan().ContainsAnyExcept(Unreserved);
}
