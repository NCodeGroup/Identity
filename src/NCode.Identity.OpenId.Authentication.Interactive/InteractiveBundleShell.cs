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

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The single-page bundle's <c>index.html</c> split once at its empty <c>#root</c> mount point, so a server-rendered
/// page is written as the head, the page's markup, and the tail without composing the document as one string.
/// </summary>
/// <param name="Head">The document up to and including the mount point's opening tag.</param>
/// <param name="Tail">The document from the mount point's closing tag to its end.</param>
internal sealed record InteractiveBundleShell(ReadOnlyMemory<char> Head, ReadOnlyMemory<char> Tail)
{
    // The empty SPA mount point; the server form is written inside it so the React SPA replaces it on mount rather than
    // rendering alongside it.
    private const string RootElement = "<div id=\"root\"></div>";
    private const string RootOpenTag = "<div id=\"root\">";

    /// <summary>
    /// Splits <paramref name="html"/> at its first empty <c>#root</c> mount point.
    /// </summary>
    /// <param name="html">The bundle's <c>index.html</c>.</param>
    /// <returns>The split document, or <c>null</c> when the document has no empty mount point.</returns>
    public static InteractiveBundleShell? TryCreate(string html)
    {
        var index = html.IndexOf(RootElement, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var headLength = index + RootOpenTag.Length;
        return new InteractiveBundleShell(html.AsMemory(0, headLength), html.AsMemory(headLength));
    }
}
