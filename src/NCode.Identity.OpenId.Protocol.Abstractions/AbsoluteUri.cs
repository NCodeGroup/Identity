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
/// Parses the absolute URIs that OAuth 2.0 and OpenID Connect messages, metadata and settings carry (RFC 3986 §4.3): text
/// that names its scheme and contains no whitespace. <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> with
/// <see cref="UriKind.Absolute"/> also accepts an implicit file path as a <c>file</c> URI — <c>/path</c> on Unix,
/// <c>C:\path</c> on Windows — and trims surrounding whitespace, so the same text would mean different things on
/// different platforms; this parser accepts neither.
/// </summary>
[PublicAPI]
public static class AbsoluteUri
{
    /// <summary>
    /// Parses the specified text as an absolute URI that names its scheme and contains no whitespace.
    /// </summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="uri">When this method returns <see langword="true"/>, the parsed URI.</param>
    /// <returns><see langword="true"/> when the text is such an absolute URI; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out Uri? uri)
    {
        if (
            !string.IsNullOrEmpty(value)
            && !value.Any(char.IsWhiteSpace)
            && Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && value.StartsWith($"{parsed.Scheme}:", StringComparison.OrdinalIgnoreCase)
        )
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }
}
