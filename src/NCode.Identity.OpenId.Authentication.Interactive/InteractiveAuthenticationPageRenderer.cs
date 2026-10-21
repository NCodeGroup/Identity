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
using System.Text.Encodings.Web;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Renders the <strong>no-JavaScript</strong> login and consent forms as server-side HTML. The forms carry the same
/// stable element ids and field names the React <c>apps/auth</c> surface uses, so the server-rendered markup is a
/// working baseline on its own: when the single-page bundle is present the form is injected into its <c>#root</c>
/// element and the SPA replaces it on mount (progressive enhancement); when the bundle is absent the form is served
/// standalone. This is what makes the interactive surface driveable by clients that do not execute JavaScript —
/// assistive technology, hardened browsers, and the OpenID conformance suite's headless browser.
/// </summary>
// Each page is written segment by segment into an IBufferWriter<char> — the bundle's head, the page's markup, and the
// bundle's tail — so neither the form nor the combined document is ever composed as a string.
internal static class InteractiveAuthenticationPageRenderer
{
    private const string StandaloneHeadStart =
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\" />"
        + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" /><title>";

    private const string StandaloneHeadEnd = "</title></head><body><div id=\"root\">";
    private const string StandaloneTail = "</div></body></html>";

    /// <summary>A single requested scope the consent form presents as a checkbox. <paramref name="Checked"/> is the
    /// default selection state — a consent screen pre-selects the requested scopes so <c>Allow</c> authorizes the
    /// request, and the subject unchecks a scope to down-scope the grant.</summary>
    internal readonly record struct ScopeView(string Value, string? Description, bool Checked);

    /// <summary>
    /// Renders the login page: a native form POST to the login endpoint carrying <c>username</c>, <c>password</c>, the
    /// return URL, and the antiforgery token, with the stable <c>username</c> / <c>password</c> / <c>login</c> ids.
    /// </summary>
    public static void RenderLoginPage(
        IBufferWriter<char> writer,
        InteractiveBundleShell? shell,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string? errorMessage
    )
    {
        WriteHead(writer, shell, "Sign in");
        Write(
            writer,
            // The action is relative to the page, so the form posts to the login endpoint of the tenant that served it.
            "<main class=\"auth-card\"><form method=\"post\" action=\"login\"><h1>Sign in</h1>"
        );
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Write(writer, "<p id=\"error\" role=\"alert\" class=\"auth-error\">");
            WriteEncoded(writer, errorMessage);
            Write(writer, "</p>");
        }
        WriteHiddenFields(writer, returnUrl, antiforgeryFieldName, antiforgeryToken);
        Write(
            writer,
            "<p><label for=\"username\">Username</label><br />"
                + "<input id=\"username\" name=\"username\" type=\"text\" autocomplete=\"username\" autofocus required /></p>"
                + "<p><label for=\"password\">Password</label><br />"
                + "<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"current-password\" required /></p>"
                + "<button id=\"login\" name=\"login\" value=\"login\" type=\"submit\">Sign in</button>"
                + "</form></main>"
        );
        WriteTail(writer, shell);
    }

    /// <summary>
    /// Renders the consent page: a per-scope checkbox over the requested scopes and allow/deny submit buttons with the
    /// stable <c>consent-allow</c> / <c>consent-deny</c> ids, posting to the consent endpoint.
    /// </summary>
    public static void RenderConsentPage(
        IBufferWriter<char> writer,
        InteractiveBundleShell? shell,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string appName,
        IReadOnlyList<ScopeView> scopes
    )
    {
        WriteHead(writer, shell, "Authorize access");
        Write(
            writer,
            "<main class=\"auth-card\"><form method=\"post\" action=\"consent\"><h1>Authorize access</h1><p>"
        );
        WriteEncoded(writer, appName);
        Write(writer, " is requesting access to your account. Choose what to allow:</p>");
        foreach (var scope in scopes)
        {
            var label = string.IsNullOrEmpty(scope.Description) ? scope.Value : scope.Description;
            Write(writer, "<p><label><input type=\"checkbox\" name=\"scope\" value=\"");
            WriteEncoded(writer, scope.Value);
            Write(writer, scope.Checked ? "\" checked /> " : "\" /> ");
            WriteEncoded(writer, label);
            Write(writer, "</label></p>");
        }
        WriteHiddenFields(writer, returnUrl, antiforgeryFieldName, antiforgeryToken);
        Write(
            writer,
            "<button id=\"consent-deny\" name=\"action\" value=\"deny\" type=\"submit\">Deny</button>"
                + "<button id=\"consent-allow\" name=\"action\" value=\"allow\" type=\"submit\">Allow</button>"
                + "</form></main>"
        );
        WriteTail(writer, shell);
    }

    /// <summary>
    /// Renders the non-redirectable authorization error page shown to the resource owner when the authorize endpoint
    /// cannot safely redirect the error back to the client (an unregistered <c>redirect_uri</c> or an unresolvable
    /// <c>client_id</c>; OpenID Connect Core §3.1.2.4). The error code is rendered as the text of a stable
    /// <c>authorization-error</c> element (plus a <c>data-error-code</c> attribute) so automation — including the
    /// OpenID conformance suite's <c>oidcc-ensure-registered-redirect-uri</c> browser task — can assert on it.
    /// </summary>
    public static void RenderErrorPage(
        IBufferWriter<char> writer,
        InteractiveBundleShell? shell,
        string errorCode,
        string? errorDescription
    )
    {
        WriteHead(writer, shell, "Authorization error");
        Write(
            writer,
            "<main class=\"auth-card\"><h1>Authorization error</h1>"
                + "<p id=\"authorization-error\" role=\"alert\" class=\"auth-error\" data-error-code=\""
        );
        WriteEncoded(writer, errorCode);
        Write(writer, "\">");
        WriteEncoded(writer, errorCode);
        Write(writer, "</p>");
        if (!string.IsNullOrEmpty(errorDescription))
        {
            Write(
                writer,
                "<p id=\"authorization-error-description\" class=\"auth-error-description\">"
            );
            WriteEncoded(writer, errorDescription);
            Write(writer, "</p>");
        }
        Write(writer, "</main>");
        WriteTail(writer, shell);
    }

    private static void WriteHiddenFields(
        IBufferWriter<char> writer,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken
    )
    {
        Write(writer, "<input type=\"hidden\" name=\"ReturnUrl\" value=\"");
        WriteEncoded(writer, returnUrl);
        Write(writer, "\" /><input type=\"hidden\" name=\"");
        WriteEncoded(writer, antiforgeryFieldName);
        Write(writer, "\" value=\"");
        WriteEncoded(writer, antiforgeryToken);
        Write(writer, "\" />");
    }

    // Writes the bundle's head, which ends inside its #root, so the SPA enhances the form on mount; without the bundle,
    // a minimal standalone document keeps the form working.
    private static void WriteHead(
        IBufferWriter<char> writer,
        InteractiveBundleShell? shell,
        string title
    )
    {
        if (shell is not null)
        {
            Write(writer, shell.Head.Span);
            return;
        }

        Write(writer, StandaloneHeadStart);
        WriteEncoded(writer, title);
        Write(writer, StandaloneHeadEnd);
    }

    private static void WriteTail(IBufferWriter<char> writer, InteractiveBundleShell? shell) =>
        Write(writer, shell is not null ? shell.Tail.Span : StandaloneTail);

    private static void Write(IBufferWriter<char> writer, ReadOnlySpan<char> value) =>
        writer.Write(value);

    private static void WriteEncoded(IBufferWriter<char> writer, ReadOnlySpan<char> value)
    {
        var encoder = HtmlEncoder.Default;
        var sizeHint = value.Length;
        while (true)
        {
            var status = encoder.Encode(
                value,
                writer.GetSpan(sizeHint),
                out var consumed,
                out var written,
                isFinalBlock: true
            );
            writer.Advance(written);
            value = value[consumed..];

            if (status == OperationStatus.Done)
            {
                return;
            }

            // An encoded character can take several characters, so ask for more room than the input still needs.
            sizeHint = Math.Max(sizeHint * 2, 16);
        }
    }
}
