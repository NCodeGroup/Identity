// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using NCode.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Renders the <strong>no-JavaScript</strong> login and consent forms as server-side HTML. The forms carry the same
/// stable element ids and field names the React <c>apps/auth</c> surface uses, so the server-rendered markup is a
/// working baseline on its own: when the single-page bundle is present the form is injected into its <c>#root</c>
/// element and the SPA replaces it on mount (progressive enhancement); when the bundle is absent the form is served
/// standalone. This is what makes the interactive surface driveable by clients that do not execute JavaScript —
/// assistive technology, hardened browsers, and the OpenID conformance suite's headless browser. Every page is
/// written into an <see cref="InteractivePageFrame"/> and rendered in its <see cref="InteractiveLocale"/>: its text
/// comes from the locale's messages, its <c>html</c> element carries the locale's language tag, and its form carries
/// the tag as <c>ui_locales</c> so a page the surface redirects back to renders in the same locale.
/// </summary>
// Each page is written segment by segment into an IBufferWriter<char> — the bundle's head, the page's markup, and the
// bundle's tail — so neither the form nor the combined document is ever composed as a string. See ADR-1402 for the
// locale.
internal static class InteractiveAuthenticationPageRenderer
{
    /// <summary>
    /// The name of the form field and the query parameter that carry the language tag of the locale a page rendered
    /// in, the same name as the authorization request's parameter.
    /// </summary>
    public const string UiLocalesFieldName = OpenIdProtocol.Parameters.UiLocales;

    private const string ConsentPromptPlaceholder = "{0}";

    private const string StandaloneHeadStart = "<!doctype html><html lang=\"";
    private const string StandaloneHeadAfterLanguage = "\"><head><meta charset=\"utf-8\" />";

    private const string StandaloneTitleStart =
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" /><title>";

    private const string StandaloneHeadEnd = "</title></head><body><div id=\"root\">";
    private const string StandaloneTail = "</div></body></html>";

    private const string BrandingStart = "<script type=\"application/json\" id=\"branding\">";
    private const string BrandingEnd = "</script>";

    /// <summary>A single requested scope the consent form presents as a checkbox. <paramref name="Checked"/> is the
    /// default selection state — a consent screen pre-selects the requested scopes so <c>Allow</c> authorizes the
    /// request, and the subject unchecks a scope to down-scope the grant.</summary>
    internal readonly record struct ScopeView(string Value, string? Description, bool Checked);

    /// <summary>
    /// Renders the login page: a native form POST to the login endpoint carrying <c>username</c>, <c>password</c>, the
    /// return URL, and the antiforgery token, with the stable <c>username</c> / <c>password</c> / <c>login</c> ids.
    /// </summary>
    /// <param name="writer">The buffer the page is written to.</param>
    /// <param name="frame">The document the page is written into.</param>
    /// <param name="returnUrl">The URL the form carries so the end-user returns to it once signed in.</param>
    /// <param name="antiforgeryFieldName">The name of the form field that carries the antiforgery token.</param>
    /// <param name="antiforgeryToken">The antiforgery token the form carries.</param>
    /// <param name="errorMessage">The message shown above the form, or <see langword="null"/> for none.</param>
    public static void RenderLoginPage(
        IBufferWriter<char> writer,
        InteractivePageFrame frame,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string? errorMessage
    )
    {
        var locale = frame.Locale;
        var messages = locale.Messages;
        WriteHead(writer, frame, messages.SignInTitle);
        // The action is relative to the page, so the form posts to the login endpoint of the tenant that served it.
        Write(writer, "<main class=\"auth-card\"><form method=\"post\" action=\"login\"><h1>");
        WriteEncoded(writer, messages.SignInTitle);
        Write(writer, "</h1>");
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Write(writer, "<p id=\"error\" role=\"alert\" class=\"auth-error\">");
            WriteEncoded(writer, errorMessage);
            Write(writer, "</p>");
        }
        WriteHiddenFields(writer, returnUrl, antiforgeryFieldName, antiforgeryToken, locale);
        Write(writer, "<p><label for=\"username\">");
        WriteEncoded(writer, messages.UsernameLabel);
        Write(
            writer,
            "</label><br />"
                + "<input id=\"username\" name=\"username\" type=\"text\" autocomplete=\"username\" autofocus required /></p>"
                + "<p><label for=\"password\">"
        );
        WriteEncoded(writer, messages.PasswordLabel);
        Write(
            writer,
            "</label><br />"
                + "<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"current-password\" required /></p>"
                + "<button id=\"login\" name=\"login\" value=\"login\" type=\"submit\">"
        );
        WriteEncoded(writer, messages.SignInButton);
        Write(writer, "</button></form></main>");
        WriteTail(writer, frame);
    }

    /// <summary>
    /// Renders the consent page: a per-scope checkbox over the requested scopes and allow/deny submit buttons with the
    /// stable <c>consent-allow</c> / <c>consent-deny</c> ids, posting to the consent endpoint.
    /// </summary>
    /// <param name="writer">The buffer the page is written to.</param>
    /// <param name="frame">The document the page is written into.</param>
    /// <param name="returnUrl">The URL the form carries so the request resumes once consent is given.</param>
    /// <param name="antiforgeryFieldName">The name of the form field that carries the antiforgery token.</param>
    /// <param name="antiforgeryToken">The antiforgery token the form carries.</param>
    /// <param name="appName">The name of the application that requests access.</param>
    /// <param name="scopes">The requested scopes, each presented as a checkbox.</param>
    public static void RenderConsentPage(
        IBufferWriter<char> writer,
        InteractivePageFrame frame,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string appName,
        IReadOnlyList<ScopeView> scopes
    )
    {
        var locale = frame.Locale;
        var messages = locale.Messages;
        WriteHead(writer, frame, messages.AuthorizeAccessTitle);
        Write(writer, "<main class=\"auth-card\"><form method=\"post\" action=\"consent\"><h1>");
        WriteEncoded(writer, messages.AuthorizeAccessTitle);
        Write(writer, "</h1><p>");
        WriteConsentPrompt(writer, messages.ConsentPromptFormat, appName);
        Write(writer, "</p>");
        foreach (var scope in scopes)
        {
            var label = string.IsNullOrEmpty(scope.Description) ? scope.Value : scope.Description;
            Write(writer, "<p><label><input type=\"checkbox\" name=\"scope\" value=\"");
            WriteEncoded(writer, scope.Value);
            Write(writer, scope.Checked ? "\" checked /> " : "\" /> ");
            WriteEncoded(writer, label);
            Write(writer, "</label></p>");
        }
        WriteHiddenFields(writer, returnUrl, antiforgeryFieldName, antiforgeryToken, locale);
        Write(
            writer,
            "<button id=\"consent-deny\" name=\"action\" value=\"deny\" type=\"submit\">"
        );
        WriteEncoded(writer, messages.DenyButton);
        Write(
            writer,
            "</button><button id=\"consent-allow\" name=\"action\" value=\"allow\" type=\"submit\">"
        );
        WriteEncoded(writer, messages.AllowButton);
        Write(writer, "</button></form></main>");
        WriteTail(writer, frame);
    }

    /// <summary>
    /// Renders the logout confirmation page: a form with stable <c>logout-confirm</c> / <c>logout-cancel</c> submit
    /// buttons, posting to the logout confirmation endpoint.
    /// </summary>
    /// <param name="writer">The buffer the page is written to.</param>
    /// <param name="frame">The document the page is written into.</param>
    /// <param name="returnUrl">The URL the form carries so the logout resumes once it is confirmed.</param>
    /// <param name="antiforgeryFieldName">The name of the form field that carries the antiforgery token.</param>
    /// <param name="antiforgeryToken">The antiforgery token the form carries.</param>
    public static void RenderLogoutConfirmationPage(
        IBufferWriter<char> writer,
        InteractivePageFrame frame,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken
    )
    {
        var locale = frame.Locale;
        var messages = locale.Messages;
        WriteHead(writer, frame, messages.SignOutTitle);
        Write(
            writer,
            "<main class=\"auth-card\"><form method=\"post\" action=\"logout/confirm\"><h1>"
        );
        WriteEncoded(writer, messages.SignOutTitle);
        Write(writer, "</h1><p>");
        WriteEncoded(writer, messages.SignOutQuestion);
        Write(writer, "</p>");
        WriteHiddenFields(writer, returnUrl, antiforgeryFieldName, antiforgeryToken, locale);
        Write(
            writer,
            "<button id=\"logout-cancel\" name=\"action\" value=\"cancel\" type=\"submit\">"
        );
        WriteEncoded(writer, messages.StaySignedInButton);
        Write(
            writer,
            "</button><button id=\"logout-confirm\" name=\"action\" value=\"logout\" type=\"submit\">"
        );
        WriteEncoded(writer, messages.SignOutButton);
        Write(writer, "</button></form></main>");
        WriteTail(writer, frame);
    }

    /// <summary>
    /// Renders the non-redirectable authorization error page shown to the resource owner when the authorize endpoint
    /// cannot safely redirect the error back to the client (an unregistered <c>redirect_uri</c> or an unresolvable
    /// <c>client_id</c>; OpenID Connect Core §3.1.2.4). The error code is rendered as the text of a stable
    /// <c>authorization-error</c> element (plus a <c>data-error-code</c> attribute) so automation — including the
    /// OpenID conformance suite's <c>oidcc-ensure-registered-redirect-uri</c> browser task — can assert on it.
    /// </summary>
    /// <param name="writer">The buffer the page is written to.</param>
    /// <param name="frame">The document the page is written into.</param>
    /// <param name="errorCode">The error code, such as <c>invalid_client</c>.</param>
    /// <param name="errorDescription">The human-readable error description, or <see langword="null"/> for none.</param>
    public static void RenderErrorPage(
        IBufferWriter<char> writer,
        InteractivePageFrame frame,
        string errorCode,
        string? errorDescription
    )
    {
        var locale = frame.Locale;
        var messages = locale.Messages;
        WriteHead(writer, frame, messages.AuthorizationErrorTitle);
        Write(writer, "<main class=\"auth-card\"><h1>");
        WriteEncoded(writer, messages.AuthorizationErrorTitle);
        Write(
            writer,
            "</h1><p id=\"authorization-error\" role=\"alert\" class=\"auth-error\" data-error-code=\""
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
        WriteTail(writer, frame);
    }

    /// <summary>
    /// Renders a page the single-page bundle draws entirely (the account, applications, and sessions pages): the
    /// bundle's document with its base set to the surface beneath the tenant and its mount point left empty.
    /// </summary>
    /// <param name="writer">The buffer the page is written to.</param>
    /// <param name="frame">The document the page is written into, whose <see cref="InteractivePageFrame.Shell"/> is
    /// the bundle's split <c>index.html</c>.</param>
    /// <exception cref="ArgumentException">The frame has no bundle.</exception>
    public static void RenderBundlePage(IBufferWriter<char> writer, InteractivePageFrame frame)
    {
        if (frame.Shell is null)
        {
            throw new ArgumentException(
                "A page the bundle draws needs the bundle's document.",
                nameof(frame)
            );
        }

        WriteHead(writer, frame, title: string.Empty);
        WriteTail(writer, frame);
    }

    // The sentence is written around its placeholder, every part encoded, so a format without the placeholder is
    // written as it is.
    private static void WriteConsentPrompt(
        IBufferWriter<char> writer,
        string consentPromptFormat,
        string appName
    )
    {
        var placeholderIndex = consentPromptFormat.IndexOf(
            ConsentPromptPlaceholder,
            StringComparison.Ordinal
        );
        if (placeholderIndex < 0)
        {
            WriteEncoded(writer, consentPromptFormat);
            return;
        }

        WriteEncoded(writer, consentPromptFormat.AsSpan(0, placeholderIndex));
        WriteEncoded(writer, appName);
        WriteEncoded(
            writer,
            consentPromptFormat.AsSpan(placeholderIndex + ConsentPromptPlaceholder.Length)
        );
    }

    private static void WriteHiddenFields(
        IBufferWriter<char> writer,
        string returnUrl,
        string antiforgeryFieldName,
        string antiforgeryToken,
        InteractiveLocale locale
    )
    {
        Write(writer, "<input type=\"hidden\" name=\"ReturnUrl\" value=\"");
        WriteEncoded(writer, returnUrl);
        Write(writer, "\" /><input type=\"hidden\" name=\"");
        WriteEncoded(writer, antiforgeryFieldName);
        Write(writer, "\" value=\"");
        WriteEncoded(writer, antiforgeryToken);
        Write(writer, "\" /><input type=\"hidden\" name=\"" + UiLocalesFieldName + "\" value=\"");
        WriteEncoded(writer, locale.LanguageTag);
        Write(writer, "\" />");
    }

    // Writes the bundle's head, which ends inside its #root, so the SPA enhances the form on mount; without the bundle,
    // a minimal standalone document keeps the form working. Either way the document's base is the surface beneath the
    // request's tenant, so the bundle's relative asset URLs, its API calls, and the forms' relative actions resolve
    // there whatever path the page itself was served at (ADR-1193). The tenant's branding follows the base, so the
    // bundle finds it in the page it mounts over. The bundle's own markup carries the nonce and the language tag in
    // place of their placeholders; no rendered value does. A tenant's tab icon is the page's only icon, written before
    // the bundle's head, which is then written without the family's icon links (ADR-1494). See ADR-1256 and ADR-1402.
    private static void WriteHead(
        IBufferWriter<char> writer,
        InteractivePageFrame frame,
        string title
    )
    {
        if (frame.Shell is { } shell)
        {
            WriteShellSegment(writer, shell.Prelude.Span, frame);
            WriteBase(writer, frame.SurfaceBase);
            if (frame.Branding is { } branding)
            {
                WriteBranding(writer, branding);
            }

            if (frame.Branding?.FaviconUri is { } faviconUri)
            {
                Write(writer, HtmlIconLinks.Create(faviconUri.AbsoluteUri));
                WriteShellSegment(writer, shell.HeadWithoutIcons.Span, frame);
            }
            else
            {
                WriteShellSegment(writer, shell.Head.Span, frame);
            }

            return;
        }

        Write(writer, StandaloneHeadStart);
        WriteEncoded(writer, frame.Locale.LanguageTag);
        Write(writer, StandaloneHeadAfterLanguage);
        WriteBase(writer, frame.SurfaceBase);
        if (frame.Branding?.FaviconUri is { } standaloneFaviconUri)
        {
            Write(writer, HtmlIconLinks.Create(standaloneFaviconUri.AbsoluteUri));
        }

        Write(writer, StandaloneTitleStart);
        WriteEncoded(writer, title);
        Write(writer, StandaloneHeadEnd);
    }

    private static void WriteBase(IBufferWriter<char> writer, string surfaceBase)
    {
        Write(writer, "<base href=\"");
        WriteEncoded(writer, surfaceBase);
        Write(writer, "\" />");
    }

    // The bundle reads this block when it mounts (ADR-1253). The default encoder of JsonSerializerOptions.Web escapes
    // `<`, `>` and `&`, so no value can close the block.
    private static void WriteBranding(IBufferWriter<char> writer, InteractiveBranding branding)
    {
        Write(writer, BrandingStart);
        Write(writer, JsonSerializer.Serialize(branding, JsonSerializerOptions.Web));
        Write(writer, BrandingEnd);
    }

    private static void WriteTail(IBufferWriter<char> writer, InteractivePageFrame frame)
    {
        if (frame.Shell is { } shell)
        {
            WriteShellSegment(writer, shell.Tail.Span, frame);
            return;
        }

        Write(writer, StandaloneTail);
    }

    // Writes a segment of the bundle's own document with the response's nonce in place of every nonce placeholder and
    // the page's language tag in place of every language placeholder, whichever comes first.
    private static void WriteShellSegment(
        IBufferWriter<char> writer,
        ReadOnlySpan<char> segment,
        InteractivePageFrame frame
    )
    {
        while (true)
        {
            var nonceIndex = segment.IndexOf(
                InteractiveBundleShell.NoncePlaceholder,
                StringComparison.Ordinal
            );
            var languageIndex = segment.IndexOf(
                InteractiveBundleShell.LanguagePlaceholder,
                StringComparison.Ordinal
            );
            if (nonceIndex < 0 && languageIndex < 0)
            {
                Write(writer, segment);
                return;
            }

            var isNonceFirst = nonceIndex >= 0 && (languageIndex < 0 || nonceIndex < languageIndex);
            var index = isNonceFirst ? nonceIndex : languageIndex;
            var placeholderLength = isNonceFirst
                ? InteractiveBundleShell.NoncePlaceholder.Length
                : InteractiveBundleShell.LanguagePlaceholder.Length;

            Write(writer, segment[..index]);
            if (isNonceFirst)
            {
                Write(writer, frame.Nonce);
            }
            else
            {
                WriteEncoded(writer, frame.Locale.LanguageTag);
            }

            segment = segment[(index + placeholderLength)..];
        }
    }

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
