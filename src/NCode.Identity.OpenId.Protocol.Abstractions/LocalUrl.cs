#region Copyright Preamble
//
//    Copyright @ 2026 NCode Group
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


using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId;

/// <summary>
/// Recognizes the local URLs a server may redirect a browser to: a path-absolute reference (RFC 3986 §4.2) that begins
/// with exactly one <c>/</c>, which every browser resolves against the current origin. Browsers strip tabs and newlines
/// and read <c>\</c> as <c>/</c>, so such characters are refused anywhere in the value.
/// </summary>
[PublicAPI]
public static class LocalUrl
{
    /// <summary>
    /// Determines whether the text is a local URL.
    /// </summary>
    /// <param name="value">The text to check.</param>
    /// <returns><see langword="true"/> when the text is a local URL; otherwise, <see langword="false"/>.</returns>
    public static bool IsLocal([NotNullWhen(true)] string? value) =>
        value is not null && IsLocal(value.AsSpan());

    /// <summary>
    /// Determines whether the text is a local URL: it begins with exactly one <c>/</c> and contains no <c>\</c>,
    /// whitespace or control character.
    /// </summary>
    /// <param name="value">The text to check.</param>
    /// <returns><see langword="true"/> when the text is a local URL; otherwise, <see langword="false"/>.</returns>
    public static bool IsLocal(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] != '/' || (value.Length > 1 && value[1] == '/'))
            return false;

        foreach (var c in value)
        {
            if (c == '\\' || char.IsWhiteSpace(c) || char.IsControl(c))
                return false;
        }

        return true;
    }
}
