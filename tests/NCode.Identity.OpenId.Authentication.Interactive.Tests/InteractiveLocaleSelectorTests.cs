// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.OpenId.Locales;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveLocaleSelectorTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IUiLocaleCatalog> MockCatalog { get; }

    public InteractiveLocaleSelectorTests()
    {
        MockCatalog = MockRepository.Create<IUiLocaleCatalog>();
    }

    public void Dispose() => MockRepository.Verify();

    #region Helpers

    private InteractiveLocaleSelector CreateSelector(params IUiLocale[] locales)
    {
        MockCatalog.SetupGet(x => x.Locales).Returns(locales).Verifiable();
        return new InteractiveLocaleSelector(MockCatalog.Object);
    }

    private static DefaultHttpContext CreateHttpContext(
        string? queryString = null,
        string? acceptLanguage = null
    )
    {
        var httpContext = new DefaultHttpContext();
        if (queryString is not null)
        {
            httpContext.Request.QueryString = new QueryString(queryString);
        }

        if (acceptLanguage is not null)
        {
            httpContext.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        return httpContext;
    }

    private static bool IsSequence(IEnumerable<string> actual, params string[] expected) =>
        actual.SequenceEqual(expected, StringComparer.Ordinal);

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenARegisteredLocaleCarriesNoMessages_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateSelector(TestLocales.Xx, new PlainLocale("it"))
        );

        Assert.Contains("'it'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Select Tests

    [Fact]
    public void Select_WhenTheRequestCarriesUiLocales_PrefersThemInOrderBeforeAcceptLanguage()
    {
        var selector = CreateSelector(TestLocales.Xx, TestLocales.Yy);
        var settings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var httpContext = CreateHttpContext(
            "?ReturnUrl=%2F&ui_locales=fr-CA%20de&ui_locales=es",
            "it;q=0.9, en;q=0.8"
        );
        MockCatalog
            .Setup(x =>
                x.Select(
                    settings,
                    It.Is<IEnumerable<string>>(preferences =>
                        IsSequence(preferences, "fr-CA", "de", "es", "it", "en")
                    )
                )
            )
            .Returns(TestLocales.Yy)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(TestLocales.Yy, locale);
    }

    [Fact]
    public void Select_WhenOnlyAcceptLanguageIsSent_OrdersItByQualityWithoutTheWildcard()
    {
        var selector = CreateSelector(TestLocales.Xx);
        var settings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var httpContext = CreateHttpContext(
            acceptLanguage: "de;q=0.5, fr, *;q=0.1, es;q=0, en-GB;q=0.8"
        );
        MockCatalog
            .Setup(x =>
                x.Select(
                    settings,
                    It.Is<IEnumerable<string>>(preferences =>
                        IsSequence(preferences, "fr", "en-GB", "de")
                    )
                )
            )
            .Returns(TestLocales.Xx)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(TestLocales.Xx, locale);
    }

    [Fact]
    public void Select_WhenAcceptLanguageTiesQualities_KeepsTheHeadersOrderAmongThem()
    {
        var selector = CreateSelector(TestLocales.Xx);
        var settings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var httpContext = CreateHttpContext(acceptLanguage: "es;q=0.7, de;q=0.7, en, fr;q=0.7, it");
        MockCatalog
            .Setup(x =>
                x.Select(
                    settings,
                    It.Is<IEnumerable<string>>(preferences =>
                        IsSequence(preferences, "en", "it", "es", "de", "fr")
                    )
                )
            )
            .Returns(TestLocales.Xx)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(TestLocales.Xx, locale);
    }

    [Fact]
    public void Select_WhenTheRequestResolvedNoTenant_SelectsWithoutSettings()
    {
        var selector = CreateSelector(TestLocales.Xx, TestLocales.Yy);
        var httpContext = CreateHttpContext("?ui_locales=de");
        MockCatalog
            .Setup(x =>
                x.Select(
                    It.Is<IEnumerable<string>>(preferences => IsSequence(preferences, "de")),
                    null,
                    null
                )
            )
            .Returns(TestLocales.Yy)
            .Verifiable();

        var locale = selector.Select(httpContext, settings: null);

        Assert.Same(TestLocales.Yy, locale);
    }

    [Fact]
    public void Select_WhenTheRequestStatesNoPreference_SelectsWithAnEmptyList()
    {
        var selector = CreateSelector(TestLocales.Xx);
        var settings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var httpContext = CreateHttpContext("?ui_locales=");
        MockCatalog
            .Setup(x =>
                x.Select(settings, It.Is<IEnumerable<string>>(preferences => !preferences.Any()))
            )
            .Returns(TestLocales.Xx)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(TestLocales.Xx, locale);
    }

    #endregion

    #region GetCarriedLanguageTag Tests

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("it", null)]
    [InlineData("YY", "yy")]
    public void GetCarriedLanguageTag_Always_KeepsOnlyARegisteredTag(
        string? uiLocales,
        string? expected
    )
    {
        var selector = CreateSelector(TestLocales.Xx, TestLocales.Yy);
        if (!string.IsNullOrEmpty(uiLocales))
        {
            IUiLocale? locale = expected is null ? null : TestLocales.Yy;
            MockCatalog
                .Setup(x => x.TryGet(uiLocales, out locale))
                .Returns(expected is not null)
                .Verifiable();
        }

        var languageTag = selector.GetCarriedLanguageTag(uiLocales);

        Assert.Equal(expected, languageTag);
    }

    #endregion

    #region AppendUiLocalesQuery Tests

    [Theory]
    [InlineData("/acme/account/login", "/acme/account/login?ui_locales=fr-CA%20de")]
    [InlineData(
        "/acme/account/login?ReturnUrl=%2F",
        "/acme/account/login?ReturnUrl=%2F&ui_locales=fr-CA%20de"
    )]
    public void AppendUiLocalesQuery_WhenThereAreTags_EscapesThemAsOneParameterAfterAnyQuery(
        string url,
        string expected
    )
    {
        var result = InteractiveLocaleSelector.AppendUiLocalesQuery(url, ["fr-CA", "de"]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendUiLocalesQuery_WhenThereAreNone_ReturnsTheUrlAsItIs()
    {
        const string url = "/acme/account/login?ReturnUrl=%2F";

        Assert.Same(url, InteractiveLocaleSelector.AppendUiLocalesQuery(url, (string?)null));
        Assert.Same(url, InteractiveLocaleSelector.AppendUiLocalesQuery(url, string.Empty));
        Assert.Same(
            url,
            InteractiveLocaleSelector.AppendUiLocalesQuery(url, (IReadOnlyList<string>?)null)
        );
        Assert.Same(url, InteractiveLocaleSelector.AppendUiLocalesQuery(url, []));
    }

    [Fact]
    public void AppendUiLocalesQuery_WhenThereIsOneTag_CarriesIt()
    {
        var result = InteractiveLocaleSelector.AppendUiLocalesQuery("/acme/account", "fr");

        Assert.Equal("/acme/account?ui_locales=fr", result);
    }

    #endregion
}
