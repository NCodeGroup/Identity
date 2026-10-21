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
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveAuthenticationPageRendererTests
{
    private const string BundleShell =
        "<!doctype html><html><head><title>t</title></head><body><div id=\"root\"></div>"
        + "<script src=\"./app.js\"></script></body></html>";

    private const string SurfaceBase = "/acme/account/";

    #region RenderLoginPage Tests

    [Fact]
    public void RenderLoginPage_Always_ContainsStableAutomationIdsAndFields()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: InteractiveBundleShell.TryCreate(BundleShell),
            surfaceBase: SurfaceBase,
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
            shell: InteractiveBundleShell.TryCreate(BundleShell),
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: SurfaceBase,
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

    #endregion

    #region RenderConsentPage Tests

    [Fact]
    public void RenderConsentPage_Always_ContainsStableAllowAndDenyIds()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderConsentPage(
            writer,
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: SurfaceBase,
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

    #region RenderErrorPage Tests

    [Fact]
    public void RenderErrorPage_Always_ContainsStableAutomationElementAndErrorCode()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderErrorPage(
            writer,
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: SurfaceBase,
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
            shell: InteractiveBundleShell.TryCreate(BundleShell),
            surfaceBase: SurfaceBase,
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
            shell: null,
            surfaceBase: "/a&b/account/",
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

    #endregion

    #region RenderBundlePage Tests

    [Fact]
    public void RenderBundlePage_Always_WritesTheBundleWithItsBaseAndAnEmptyMountPoint()
    {
        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderBundlePage(
            writer,
            InteractiveBundleShell.TryCreate(BundleShell)!,
            SurfaceBase
        );

        Assert.Equal(
            "<!doctype html><html><head><base href=\"/acme/account/\" /><title>t</title></head><body>"
                + "<div id=\"root\"></div><script src=\"./app.js\"></script></body></html>",
            writer.WrittenSpan.ToString()
        );
    }

    #endregion
}
