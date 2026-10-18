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

using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveAuthenticationPageRendererTests
{
    private const string BundleShell =
        "<!doctype html><html><body><div id=\"root\"></div><script src=\"/app.js\"></script></body></html>";

    #region RenderLoginPage Tests

    [Fact]
    public void RenderLoginPage_Always_ContainsStableAutomationIdsAndFields()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderLoginPage(
            bundleIndexHtml: null,
            returnUrl: "/connect/authorize",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "token-123",
            errorMessage: null
        );

        Assert.Contains("id=\"username\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"login\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "method=\"post\" action=\"/account/login\"",
            html,
            StringComparison.Ordinal
        );
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
        var html = InteractiveAuthenticationPageRenderer.RenderLoginPage(
            bundleIndexHtml: null,
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: "The username or password is incorrect."
        );

        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains("The username or password is incorrect.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenHostileReturnUrl_EncodesIt()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderLoginPage(
            bundleIndexHtml: null,
            returnUrl: "\"><script>alert(1)</script>",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLoginPage_WhenBundlePresent_InjectsFormIntoRoot()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderLoginPage(
            bundleIndexHtml: BundleShell,
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );

        // The empty mount point is replaced by the form, and the SPA script tag is preserved.
        Assert.DoesNotContain("<div id=\"root\"></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div id=\"root\"><main", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"/app.js\">", html, StringComparison.Ordinal);
    }

    #endregion

    #region RenderConsentPage Tests

    [Fact]
    public void RenderConsentPage_Always_ContainsStableAllowAndDenyIds()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderConsentPage(
            bundleIndexHtml: null,
            returnUrl: "/continue",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            appName: "Conformance Client",
            scopes: []
        );

        Assert.Contains("id=\"consent-allow\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"consent-deny\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "method=\"post\" action=\"/account/consent\"",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("Conformance Client", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderConsentPage_WhenScopes_RendersACheckboxPerScopeWithCheckedState()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderConsentPage(
            bundleIndexHtml: null,
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

    #region RenderErrorPage Tests

    [Fact]
    public void RenderErrorPage_Always_ContainsStableAutomationElementAndErrorCode()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderErrorPage(
            bundleIndexHtml: null,
            errorCode: "unauthorized_client",
            errorDescription: "The specified 'redirect_uri' is not valid for the associated 'client_id'."
        );

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
        var html = InteractiveAuthenticationPageRenderer.RenderErrorPage(
            bundleIndexHtml: null,
            errorCode: "invalid_client",
            errorDescription: null
        );

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
        var html = InteractiveAuthenticationPageRenderer.RenderErrorPage(
            bundleIndexHtml: null,
            errorCode: "\"><script>alert(1)</script>",
            errorDescription: null
        );

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderErrorPage_WhenBundlePresent_InjectsErrorIntoRoot()
    {
        var html = InteractiveAuthenticationPageRenderer.RenderErrorPage(
            bundleIndexHtml: BundleShell,
            errorCode: "invalid_client",
            errorDescription: null
        );

        Assert.DoesNotContain("<div id=\"root\"></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div id=\"root\"><main", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"/app.js\">", html, StringComparison.Ordinal);
    }

    #endregion
}
