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
/// The single-page bundle's <c>index.html</c> split once at the end of its <c>&lt;head&gt;</c> start tag and at its
/// empty <c>#root</c> mount point, so a served page is written as the prelude, the document's <c>&lt;base&gt;</c>, the
/// head, the page's markup, and the tail without composing the document as one string.
/// </summary>
/// <param name="Prelude">The document up to and including the <c>&lt;head&gt;</c> start tag.</param>
/// <param name="Head">The document from the end of the <c>&lt;head&gt;</c> start tag up to and including the mount
/// point's opening tag.</param>
/// <param name="Tail">The document from the mount point's closing tag to its end.</param>
internal sealed record InteractiveBundleShell(
    ReadOnlyMemory<char> Prelude,
    ReadOnlyMemory<char> Head,
    ReadOnlyMemory<char> Tail
)
{
    /// <summary>
    /// The value that the bundle's build writes into the <c>nonce</c> attribute of its scripts and of its
    /// <c>csp-nonce</c> meta element. A served page carries the response's nonce in its place.
    /// </summary>
    // See ADR-0256.
    public const string NoncePlaceholder = "__NCODE_CSP_NONCE__";

    // The empty SPA mount point; the server form is written inside it so the React SPA replaces it on mount rather than
    // rendering alongside it.
    private const string RootElement = "<div id=\"root\"></div>";
    private const string RootOpenTag = "<div id=\"root\">";
    private const string HeadTagStart = "<head";

    /// <summary>
    /// Splits <paramref name="html"/> at the end of its <c>&lt;head&gt;</c> start tag and at its first empty
    /// <c>#root</c> mount point.
    /// </summary>
    /// <param name="html">The bundle's <c>index.html</c>.</param>
    /// <returns>The split document, or <c>null</c> when the document has no <c>&lt;head&gt;</c> start tag before an
    /// empty mount point.</returns>
    public static InteractiveBundleShell? TryCreate(string html)
    {
        var rootIndex = html.IndexOf(RootElement, StringComparison.Ordinal);
        if (rootIndex < 0)
        {
            return null;
        }

        var preludeLength = FindHeadStartTagEnd(html, rootIndex);
        if (preludeLength < 0)
        {
            return null;
        }

        var headEnd = rootIndex + RootOpenTag.Length;
        return new InteractiveBundleShell(
            html.AsMemory(0, preludeLength),
            html.AsMemory(preludeLength, headEnd - preludeLength),
            html.AsMemory(headEnd)
        );
    }

    // The base must precede every element whose URL it resolves, so it is written first inside <head>. The tag name is
    // matched whole, so a <header> element is not mistaken for it.
    private static int FindHeadStartTagEnd(string html, int limit)
    {
        var index = 0;
        while (true)
        {
            index = html.IndexOf(
                HeadTagStart,
                index,
                limit - index,
                StringComparison.OrdinalIgnoreCase
            );
            if (index < 0)
            {
                return -1;
            }

            var next = index + HeadTagStart.Length;
            if (next < limit && (html[next] == '>' || char.IsWhiteSpace(html[next])))
            {
                var close = html.IndexOf('>', next, limit - next);
                return close < 0 ? -1 : close + 1;
            }

            index = next;
        }
    }
}
