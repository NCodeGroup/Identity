// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveAuthenticationPageRendererTests
{
    private const string BundleShell =
        "<!doctype html><html><head><title>t</title></head><body><div id=\"root\"></div>"
        + "<script src=\"./app.js\"></script></body></html>";

    private const string SurfaceBase = "/acme/account/";

    private const string Nonce = "n0nce";

    private const string NonceBundleShell =
        "<!doctype html><html><head><meta property=\"csp-nonce\" nonce=\"__NCODE_CSP_NONCE__\">"
        + "<script type=\"module\" src=\"./app.js\" nonce=\"__NCODE_CSP_NONCE__\"></script></head>"
        + "<body><div id=\"root\"></div></body></html>";

    private const string LanguageBundleShell =
        "<!doctype html><html lang=\"__NCODE_LANG__\"><head><meta property=\"csp-nonce\" nonce=\"__NCODE_CSP_NONCE__\"></head>"
        + "<body><div id=\"root\"></div></body></html>";

    private static InteractiveBranding AcmeBranding { get; } =
        new("Acme", null, null, null, null, "dark", null);

    private static InteractivePageFrame CreateFrame(
        InteractiveBundleShell? shell = null,
        string surfaceBase = SurfaceBase,
        InteractiveBranding? branding = null,
        InteractiveLocale? locale = null
    ) => new(shell, surfaceBase, branding, Nonce, locale ?? TestLocales.Xx);

    #region RenderLoginPage Tests

    [Fact]
    public void RenderLoginPage_Always_ContainsStableAutomationIdsAndFields()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(),
            returnUrl: "/connect/authorize",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "token-123",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("id=\"username\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"login\"", html, StringComparison.Ordinal);
        Assert.Contains("method=\"post\" action=\"login\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "name=\"ReturnUrl\" value=\"/connect/authorize\"",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "name=\"__RequestVerificationToken\" value=\"token-123\"",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderLoginPage_WhenErrorMessage_RendersIt()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: "The username or password is incorrect."
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains("The username or password is incorrect.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenHostileReturnUrl_EncodesIt()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(),
            returnUrl: "\"><script>alert(1)</script>",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenBundlePresent_InjectsFormIntoRoot()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(shell: InteractiveBundleShell.TryCreate(BundleShell)),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        // The empty mount point is replaced by the form, and the SPA script tag is preserved.
        Assert.DoesNotContain("<div id=\"root\"></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div id=\"root\"><main", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"./app.js\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenBundlePresent_WritesTheFormBetweenTheBundleHeadAndTail()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(shell: InteractiveBundleShell.TryCreate(BundleShell)),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        // The document's base is written first inside <head>, so it resolves the bundle's relative asset URLs.
        Assert.StartsWith(
            "<!doctype html><html><head><base href=\"/acme/account/\" /><title>t</title></head>"
                + "<body><div id=\"root\"><main",
            html,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            "</main></div><script src=\"./app.js\"></script></body></html>",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderLoginPage_WhenTheEncodedValueOutgrowsTheBuffer_EncodesAllOfIt()
    {
        var writer = new ArrayBufferWriter<char>(initialCapacity: 1);
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: new string('<', 1000)
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains(
            $"class=\"auth-error\">{string.Concat(Enumerable.Repeat("&lt;", 1000))}</p>",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderLoginPage_WhenBundlePresent_EmbedsTheBrandingAfterTheBase()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(
                shell: InteractiveBundleShell.TryCreate(BundleShell),
                branding: AcmeBranding
            ),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.StartsWith(
            "<!doctype html><html><head><base href=\"/acme/account/\" />"
                + "<script type=\"application/json\" id=\"branding\">{\"displayName\":\"Acme\",\"mode\":\"dark\"}</script>"
                + "<title>t</title></head>",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderLoginPage_WhenTheBrandingHoldsMarkup_EscapesItInsideTheDataBlock()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(
                shell: InteractiveBundleShell.TryCreate(BundleShell),
                branding: new InteractiveBranding(
                    "</script><script>alert(1)</script>",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                )
            ),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.DoesNotContain("</script><script>alert(1)", html, StringComparison.Ordinal);
        Assert.Contains("\\u003C/script\\u003E", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenBundleIsAbsent_EmbedsNoBranding()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(branding: AcmeBranding),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.DoesNotContain("id=\"branding\"", html, StringComparison.Ordinal);
    }

    private const string IconBundleShell =
        "<!doctype html><html><head><link rel=\"icon\" href=\"./assets/favicon-ab12.ico\" sizes=\"32x32\">"
        + "<link rel=\"apple-touch-icon\" href=\"./assets/apple-touch-icon-ab12.png\"><title>t</title></head>"
        + "<body><div id=\"root\"></div></body></html>";

    private static string RenderLoginPageHtml(InteractiveBundleShell? shell, string? faviconUrl)
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(
                shell: shell,
                branding: new InteractiveBranding("Acme", null, faviconUrl, null, null, null, null)
            ),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        return writer.WrittenSpan.ToString();
    }

    [Fact]
    public void RenderLoginPage_WhenTheTenantSetsAFavicon_LinksItInPlaceOfTheFamilysIcons()
    {
        var html = RenderLoginPageHtml(
            InteractiveBundleShell.TryCreate(IconBundleShell),
            "https://cdn.acme.example/tab.ico"
        );

        Assert.Contains(
            "<link rel=\"icon\" href=\"https://cdn.acme.example/tab.ico\" />",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("<title>t</title>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("favicon-ab12", html, StringComparison.Ordinal);
        Assert.DoesNotContain("apple-touch-icon", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenTheTenantSetsNoFavicon_KeepsTheFamilysIcons()
    {
        var html = RenderLoginPageHtml(InteractiveBundleShell.TryCreate(IconBundleShell), null);

        Assert.Contains("favicon-ab12", html, StringComparison.Ordinal);
        Assert.Contains("apple-touch-icon", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.acme.example", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenTheFaviconIsNotHttps_KeepsTheFamilysIcons()
    {
        var html = RenderLoginPageHtml(
            InteractiveBundleShell.TryCreate(IconBundleShell),
            "http://cdn.acme.example/tab.ico"
        );

        Assert.Contains("favicon-ab12", html, StringComparison.Ordinal);
        Assert.Contains("apple-touch-icon", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.acme.example", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenBundleIsAbsentAndTheTenantSetsAFavicon_LinksItOnce()
    {
        var html = RenderLoginPageHtml(null, "https://cdn.acme.example/tab.ico");

        Assert.Equal(1, CountOccurrences(html, "<link rel=\"icon\""));
        Assert.Contains(
            "<link rel=\"icon\" href=\"https://cdn.acme.example/tab.ico\" />",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderLoginPage_WhenTheTenantSetsAFavicon_WritesTheConfiguredTextAsTheHref()
    {
        var html = RenderLoginPageHtml(null, "https://CDN.acme.example");

        Assert.Contains(
            "<link rel=\"icon\" href=\"https://CDN.acme.example\" />",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderLoginPage_WhenBundleIsAbsentAndTheTenantSetsNoFavicon_LinksNoIcon()
    {
        var html = RenderLoginPageHtml(null, null);

        Assert.DoesNotContain("<link", html, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    #endregion

    #region RenderConsentPage Tests

    [Fact]
    public void RenderConsentPage_Always_ContainsStableAllowAndDenyIds()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderConsentPage(
            writer,
            CreateFrame(),
            returnUrl: "/continue",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            appName: "Conformance Client",
            scopes: []
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("id=\"consent-allow\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"consent-deny\"", html, StringComparison.Ordinal);
        Assert.Contains("method=\"post\" action=\"consent\"", html, StringComparison.Ordinal);
        Assert.Contains("Conformance Client", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderConsentPage_WhenScopes_RendersACheckboxPerScopeWithCheckedState()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderConsentPage(
            writer,
            CreateFrame(),
            returnUrl: "/continue",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            appName: "App",
            scopes:
            [
                new InteractiveAuthenticationPageRenderer.ScopeView("openid", "Sign you in", true),
                new InteractiveAuthenticationPageRenderer.ScopeView("email", null, false),
            ]
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains(
            "type=\"checkbox\" name=\"scope\" value=\"openid\" checked",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("Sign you in", html, StringComparison.Ordinal);
        Assert.Contains(
            "type=\"checkbox\" name=\"scope\" value=\"email\" />",
            html,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region RenderLogoutConfirmationPage Tests

    [Fact]
    public void RenderLogoutConfirmationPage_Always_ContainsStableConfirmAndCancelIds()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLogoutConfirmationPage(
            writer,
            CreateFrame(),
            returnUrl: "/continue",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t"
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("id=\"logout-confirm\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"logout-cancel\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "method=\"post\" action=\"logout/confirm\"",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("value=\"logout\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"cancel\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLogoutConfirmationPage_WhenHostileReturnUrl_EncodesIt()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLogoutConfirmationPage(
            writer,
            CreateFrame(),
            returnUrl: "\"><script>alert(1)</script>",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t"
        );
        var html = writer.WrittenSpan.ToString();

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    #endregion

    #region RenderErrorPage Tests

    [Fact]
    public void RenderErrorPage_Always_ContainsStableAutomationElementAndErrorCode()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(),
            errorCode: "unauthorized_client",
            errorDescription: "The specified 'redirect_uri' is not valid for the associated 'client_id'."
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("id=\"authorization-error\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains("data-error-code=\"unauthorized_client\"", html, StringComparison.Ordinal);
        Assert.Contains("unauthorized_client", html, StringComparison.Ordinal);
        Assert.Contains(
            "The specified &#x27;redirect_uri&#x27; is not valid for the associated &#x27;client_id&#x27;.",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderErrorPage_WhenNoDescription_OmitsDescriptionElement()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(),
            errorCode: "invalid_client",
            errorDescription: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("id=\"authorization-error\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "id=\"authorization-error-description\"",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderErrorPage_WhenHostileErrorCode_EncodesIt()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(),
            errorCode: "\"><script>alert(1)</script>",
            errorDescription: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderErrorPage_WhenBundlePresent_InjectsErrorIntoRoot()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(shell: InteractiveBundleShell.TryCreate(BundleShell)),
            errorCode: "invalid_client",
            errorDescription: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.DoesNotContain("<div id=\"root\"></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div id=\"root\"><main", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"./app.js\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderErrorPage_WhenBundleIsAbsent_WritesTheSurfaceBase()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(surfaceBase: "/a&b/account/"),
            errorCode: "invalid_client",
            errorDescription: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains(
            "<head><meta charset=\"utf-8\" /><base href=\"/a&amp;b/account/\" />",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RenderErrorPage_WhenTheDescriptionCarriesTheNoncePlaceholder_WritesItAsText()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(shell: InteractiveBundleShell.TryCreate(NonceBundleShell)),
            errorCode: "invalid_client",
            errorDescription: "__NCODE_CSP_NONCE__"
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains(
            "class=\"auth-error-description\">__NCODE_CSP_NONCE__</p>",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("nonce=\"n0nce\"", html, StringComparison.Ordinal);
    }

    #endregion

    #region RenderBundlePage Tests

    [Fact]
    public void RenderBundlePage_Always_WritesTheBundleWithItsBaseAndAnEmptyMountPoint()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderBundlePage(
            writer,
            CreateFrame(shell: InteractiveBundleShell.TryCreate(BundleShell))
        );

        Assert.Equal(
            "<!doctype html><html><head><base href=\"/acme/account/\" /><title>t</title></head><body>"
                + "<div id=\"root\"></div><script src=\"./app.js\"></script></body></html>",
            writer.WrittenSpan.ToString()
        );
    }

    [Fact]
    public void RenderBundlePage_WhenTheFrameHasNoBundle_Throws()
    {
        var writer = new ArrayBufferWriter<char>();

        var exception = Assert.Throws<ArgumentException>(() =>
            InteractiveAuthenticationPageRenderer.RenderBundlePage(writer, CreateFrame())
        );

        Assert.Equal("frame", exception.ParamName);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void RenderBundlePage_WhenBrandingIsGiven_EmbedsItAfterTheBase()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderBundlePage(
            writer,
            CreateFrame(
                shell: InteractiveBundleShell.TryCreate(BundleShell),
                branding: AcmeBranding
            )
        );

        Assert.Equal(
            "<!doctype html><html><head><base href=\"/acme/account/\" />"
                + "<script type=\"application/json\" id=\"branding\">{\"displayName\":\"Acme\",\"mode\":\"dark\"}</script>"
                + "<title>t</title></head><body><div id=\"root\"></div><script src=\"./app.js\"></script></body></html>",
            writer.WrittenSpan.ToString()
        );
    }

    [Fact]
    public void RenderBundlePage_WhenTheBundleCarriesTheNoncePlaceholder_WritesTheNonceInItsPlace()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderBundlePage(
            writer,
            CreateFrame(shell: InteractiveBundleShell.TryCreate(NonceBundleShell))
        );

        Assert.Equal(
            "<!doctype html><html><head><base href=\"/acme/account/\" />"
                + "<meta property=\"csp-nonce\" nonce=\"n0nce\">"
                + "<script type=\"module\" src=\"./app.js\" nonce=\"n0nce\"></script></head>"
                + "<body><div id=\"root\"></div></body></html>",
            writer.WrittenSpan.ToString()
        );
    }

    #endregion

    #region Locale Tests

    [Fact]
    public void RenderLoginPage_WhenStandalone_WritesTheLocaleIntoTheDocumentAndTheForm()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: TestLocales.Xx.Messages.InvalidCredentialsError
        );
        var html = writer.WrittenSpan.ToString();

        Assert.StartsWith(
            "<!doctype html><html lang=\"xx\"><head>",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("<title>Xx sign-in title</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Xx sign-in title</h1>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<label for=\"username\">Xx username</label>",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<label for=\"password\">Xx password</label>",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains(">Xx sign-in button</button>", html, StringComparison.Ordinal);
        Assert.Contains("Xx invalid credentials", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ui_locales\" value=\"xx\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign in", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenTheShellCarriesTheLanguagePlaceholder_WritesTheLocaleInItsPlace()
    {
        var shell = InteractiveBundleShell.TryCreate(LanguageBundleShell);
        Assert.NotNull(shell);
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            CreateFrame(shell: shell),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.StartsWith(
            "<!doctype html><html lang=\"xx\"><head>",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("nonce=\"n0nce\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            InteractiveBundleShell.LanguagePlaceholder,
            html,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            InteractiveBundleShell.NoncePlaceholder,
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("<h1>Xx sign-in title</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderConsentPage_Always_PlacesTheEncodedApplicationNameInTheLocalesPrompt()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderConsentPage(
            writer,
            CreateFrame(),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            appName: "<Acme>",
            scopes: []
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("<h1>Xx authorize title</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<p>&lt;Acme&gt; xx consent prompt:</p>", html, StringComparison.Ordinal);
        Assert.Contains(">Xx deny</button>", html, StringComparison.Ordinal);
        Assert.Contains(">Xx allow</button>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ui_locales\" value=\"xx\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderConsentPage_WhenTheFormatHasNoPlaceholder_WritesItAsItIs()
    {
        var locale = TestLocales.Xx with
        {
            Messages = TestLocales.Xx.Messages with
            {
                ConsentPromptFormat = "Choose what to allow:",
            },
        };
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderConsentPage(
            writer,
            CreateFrame(locale: locale),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            appName: "Acme",
            scopes: []
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("<p>Choose what to allow:</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLogoutConfirmationPage_Always_WritesTheLocalesText()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLogoutConfirmationPage(
            writer,
            CreateFrame(),
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t"
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("<html lang=\"xx\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Xx sign-out titl&#xE9;</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Xx sign-out question?", html, StringComparison.Ordinal);
        Assert.Contains(">Xx stay sign&#xE9;d in</button>", html, StringComparison.Ordinal);
        Assert.Contains(">Xx sign-out button</button>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ui_locales\" value=\"xx\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderErrorPage_Always_WritesTheLocalesTitle()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            CreateFrame(),
            errorCode: "invalid_client",
            errorDescription: null
        );
        var html = writer.WrittenSpan.ToString();

        Assert.Contains("<html lang=\"xx\">", html, StringComparison.Ordinal);
        Assert.Contains("<title>Xx error title</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Xx error title</h1>", html, StringComparison.Ordinal);
    }

    #endregion
}
