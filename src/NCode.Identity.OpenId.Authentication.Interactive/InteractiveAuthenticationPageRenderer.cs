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

using System.Globalization;
using System.Text;
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
internal static class InteractiveAuthenticationPageRenderer
{
    // The empty SPA mount point in the bundle's index.html; the server form is injected here so the React SPA replaces
    // it on mount rather than rendering alongside it.
    private const string RootElement = "<div id=\"root\"></div>";

    /// <summary>A single requested scope the consent form presents as a checkbox. <paramref name="Checked"/> is the
    /// default selection state — a consent screen pre-selects the requested scopes so <c>Allow</c> authorizes the
    /// request, and the subject unchecks a scope to down-scope the grant.</summary>
    internal readonly record struct ScopeView(string Value, string? Description, bool Checked);

    /// <summary>
    /// Renders the login page: a native form POST to the login endpoint carrying <c>username</c>, <c>password</c>, the
    /// return URL, and the antiforgery token, with the stable <c>username</c> / <c>password</c> / <c>login</c> ids.
    /// </summary>
    public static string RenderLoginPage(
        string? bundleIndexHtml,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string? errorMessage
    )
    {
        var html = HtmlEncoder.Default;
        var form = new StringBuilder();
        form.Append("<main class=\"auth-card\">");
        form.Append(
            CultureInfo.InvariantCulture,
            $"<form method=\"post\" action=\"{InteractiveAuthenticationDefaults.LoginPath}\">"
        );
        form.Append("<h1>Sign in</h1>");
        if (!string.IsNullOrEmpty(errorMessage))
        {
            form.Append(
                CultureInfo.InvariantCulture,
                $"<p id=\"error\" role=\"alert\" class=\"auth-error\">{html.Encode(errorMessage)}</p>"
            );
        }
        AppendHiddenFields(form, returnUrl, antiforgeryFieldName, antiforgeryToken, html);
        form.Append(
            "<p><label for=\"username\">Username</label><br />"
                + "<input id=\"username\" name=\"username\" type=\"text\" autocomplete=\"username\" autofocus required /></p>"
        );
        form.Append(
            "<p><label for=\"password\">Password</label><br />"
                + "<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"current-password\" required /></p>"
        );
        form.Append(
            "<button id=\"login\" name=\"login\" value=\"login\" type=\"submit\">Sign in</button>"
        );
        form.Append("</form></main>");

        return Compose(bundleIndexHtml, form.ToString(), "Sign in");
    }

    /// <summary>
    /// Renders the consent page: a per-scope checkbox over the requested scopes and allow/deny submit buttons with the
    /// stable <c>consent-allow</c> / <c>consent-deny</c> ids, posting to the consent endpoint.
    /// </summary>
    public static string RenderConsentPage(
        string? bundleIndexHtml,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string appName,
        IReadOnlyList<ScopeView> scopes
    )
    {
        var html = HtmlEncoder.Default;
        var form = new StringBuilder();
        form.Append("<main class=\"auth-card\">");
        form.Append(
            CultureInfo.InvariantCulture,
            $"<form method=\"post\" action=\"{InteractiveAuthenticationDefaults.ConsentPath}\">"
        );
        form.Append("<h1>Authorize access</h1>");
        form.Append(
            CultureInfo.InvariantCulture,
            $"<p>{html.Encode(appName)} is requesting access to your account. Choose what to allow:</p>"
        );
        foreach (var scope in scopes)
        {
            var label = string.IsNullOrEmpty(scope.Description) ? scope.Value : scope.Description;
            var checkedAttr = scope.Checked ? " checked" : string.Empty;
            form.Append(
                CultureInfo.InvariantCulture,
                $"<p><label><input type=\"checkbox\" name=\"scope\" value=\"{html.Encode(scope.Value)}\"{checkedAttr} /> {html.Encode(label)}</label></p>"
            );
        }
        AppendHiddenFields(form, returnUrl, antiforgeryFieldName, antiforgeryToken, html);
        form.Append(
            "<button id=\"consent-deny\" name=\"action\" value=\"deny\" type=\"submit\">Deny</button>"
        );
        form.Append(
            "<button id=\"consent-allow\" name=\"action\" value=\"allow\" type=\"submit\">Allow</button>"
        );
        form.Append("</form></main>");

        return Compose(bundleIndexHtml, form.ToString(), "Authorize access");
    }

    /// <summary>
    /// Renders the non-redirectable authorization error page shown to the resource owner when the authorize endpoint
    /// cannot safely redirect the error back to the client (an unregistered <c>redirect_uri</c> or an unresolvable
    /// <c>client_id</c>; OpenID Connect Core §3.1.2.4). The error code is rendered as the text of a stable
    /// <c>authorization-error</c> element (plus a <c>data-error-code</c> attribute) so automation — including the
    /// OpenID conformance suite's <c>oidcc-ensure-registered-redirect-uri</c> browser task — can assert on it.
    /// </summary>
    public static string RenderErrorPage(
        string? bundleIndexHtml,
        string errorCode,
        string? errorDescription
    )
    {
        var html = HtmlEncoder.Default;
        var form = new StringBuilder();
        form.Append("<main class=\"auth-card\">");
        form.Append("<h1>Authorization error</h1>");
        form.Append(
            CultureInfo.InvariantCulture,
            $"<p id=\"authorization-error\" role=\"alert\" class=\"auth-error\" data-error-code=\"{html.Encode(errorCode)}\">{html.Encode(errorCode)}</p>"
        );
        if (!string.IsNullOrEmpty(errorDescription))
        {
            form.Append(
                CultureInfo.InvariantCulture,
                $"<p id=\"authorization-error-description\" class=\"auth-error-description\">{html.Encode(errorDescription)}</p>"
            );
        }
        form.Append("</main>");

        return Compose(bundleIndexHtml, form.ToString(), "Authorization error");
    }

    private static void AppendHiddenFields(
        StringBuilder form,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        HtmlEncoder html
    )
    {
        form.Append(
            CultureInfo.InvariantCulture,
            $"<input type=\"hidden\" name=\"ReturnUrl\" value=\"{html.Encode(returnUrl)}\" />"
        );
        form.Append(
            CultureInfo.InvariantCulture,
            $"<input type=\"hidden\" name=\"{html.Encode(antiforgeryFieldName)}\" value=\"{html.Encode(antiforgeryToken)}\" />"
        );
    }

    // Injects the form into the SPA bundle's #root when available (so the SPA enhances it on mount), otherwise returns
    // a minimal standalone document so the form works even without the built bundle.
    private static string Compose(string? bundleIndexHtml, string formHtml, string title)
    {
        var rooted = $"<div id=\"root\">{formHtml}</div>";
        if (
            !string.IsNullOrEmpty(bundleIndexHtml)
            && bundleIndexHtml.Contains(RootElement, StringComparison.Ordinal)
        )
        {
            return bundleIndexHtml.Replace(RootElement, rooted, StringComparison.Ordinal);
        }

        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\" />"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />"
            + $"<title>{HtmlEncoder.Default.Encode(title)}</title></head><body>{rooted}</body></html>";
    }
}
