// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.AspNetCore.Tests;

public class HtmlIconLinksTests : BaseTests
{
    #region Remove Tests

    [Theory]
    [InlineData("<link rel=\"icon\" href=\"a.ico\" sizes=\"32x32\">")]
    [InlineData("<link rel=\"apple-touch-icon\" href=\"a.png\" />")]
    [InlineData("<link rel=\"shortcut icon\" href=\"a.ico\">")]
    [InlineData("<link href=\"a.ico\" REL='ICON'>")]
    [InlineData("<LINK rel=icon href=a.ico>")]
    public void Remove_WhenTheLinkIsAnIconLink_RemovesIt(string link)
    {
        var actual = HtmlIconLinks.Remove($"<head>{link}</head>");

        Assert.Equal("<head></head>", actual);
    }

    [Theory]
    [InlineData("<link rel=\"stylesheet\" href=\"a.css\">")]
    [InlineData("<link rel=\"preload\" href=\"a.js\" as=\"script\">")]
    [InlineData("<link href=\"a.css\">")]
    public void Remove_WhenTheLinkIsNotAnIconLink_KeepsIt(string link)
    {
        var html = $"<head>{link}</head>";

        var actual = HtmlIconLinks.Remove(html);

        Assert.Equal(html, actual);
    }

    [Fact]
    public void Remove_WhenAHrefContainsAClosingAngleBracketInQuotes_KeepsTheLinkUnlessItsRelIsAnIcon()
    {
        const string stylesheet = "<link rel=\"stylesheet\" href=\"a>b.css\">";
        const string icon = "<link rel=\"icon\" href=\"a>b.ico\">";

        var actual = HtmlIconLinks.Remove($"<head>{stylesheet}{icon}</head>");

        Assert.Equal($"<head>{stylesheet}</head>", actual);
    }

    [Fact]
    public void Remove_WhenTheHeadHoldsBothKinds_RemovesOnlyTheIconLinks()
    {
        const string html =
            "<head>\n"
            + "  <meta charset=\"utf-8\">\n"
            + "  <link rel=\"icon\" href=\"./assets/favicon.ico\" sizes=\"32x32\">\n"
            + "  <link rel=\"stylesheet\" href=\"./assets/index.css\">\n"
            + "  <link rel=\"apple-touch-icon\" href=\"./assets/apple-touch-icon.png\">\n"
            + "  <link rel=\"preload\" href=\"./assets/index.js\" as=\"script\">\n"
            + "</head>";
        const string expected =
            "<head>\n"
            + "  <meta charset=\"utf-8\">\n"
            + "  \n"
            + "  <link rel=\"stylesheet\" href=\"./assets/index.css\">\n"
            + "  \n"
            + "  <link rel=\"preload\" href=\"./assets/index.js\" as=\"script\">\n"
            + "</head>";

        var actual = HtmlIconLinks.Remove(html);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Remove_WhenThereIsNoLink_ReturnsTheInputUnchanged()
    {
        const string html = "<html><head><title>Sign in</title></head><body></body></html>";

        var actual = HtmlIconLinks.Remove(html);

        Assert.Equal(html, actual);
    }

    [Fact]
    public void Remove_WhenTheTextOnlyResemblesALink_KeepsIt()
    {
        const string html = "<header><linkage rel=\"icon\"></linkage></header>";

        var actual = HtmlIconLinks.Remove(html);

        Assert.Equal(html, actual);
    }

    [Fact]
    public void Remove_WhenHtmlIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HtmlIconLinks.Remove(null!));
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WhenGivenAnHref_ReturnsAnIconLink()
    {
        var actual = HtmlIconLinks.Create("https://cdn.example/a.ico");

        Assert.Equal("<link rel=\"icon\" href=\"https://cdn.example/a.ico\" />", actual);
    }

    [Fact]
    public void Create_WhenTheHrefHoldsAnAmpersand_EncodesIt()
    {
        var actual = HtmlIconLinks.Create("https://cdn.example/a.ico?x=1&y=2");

        Assert.Equal(
            "<link rel=\"icon\" href=\"https://cdn.example/a.ico?x=1&amp;y=2\" />",
            actual
        );
    }

    #endregion
}
