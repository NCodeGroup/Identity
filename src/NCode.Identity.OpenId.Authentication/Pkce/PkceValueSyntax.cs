#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
