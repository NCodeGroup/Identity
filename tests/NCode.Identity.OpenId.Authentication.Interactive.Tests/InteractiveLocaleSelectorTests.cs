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

    private sealed record PlainLocale(string LanguageTag) : IUiLocale;

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
            CreateSelector(InteractiveLocales.English, new PlainLocale("it"))
        );

        Assert.Contains("'it'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Select Tests

    [Fact]
    public void Select_WhenTheRequestCarriesUiLocales_PrefersThemInOrderBeforeAcceptLanguage()
    {
        var selector = CreateSelector(InteractiveLocales.English, InteractiveLocales.French);
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
            .Returns(InteractiveLocales.French)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(InteractiveLocales.French, locale);
    }

    [Fact]
    public void Select_WhenOnlyAcceptLanguageIsSent_OrdersItByQualityWithoutTheWildcard()
    {
        var selector = CreateSelector(InteractiveLocales.English);
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
            .Returns(InteractiveLocales.English)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(InteractiveLocales.English, locale);
    }

    [Fact]
    public void Select_WhenTheRequestResolvedNoTenant_SelectsWithoutSettings()
    {
        var selector = CreateSelector(InteractiveLocales.English, InteractiveLocales.German);
        var httpContext = CreateHttpContext("?ui_locales=de");
        MockCatalog
            .Setup(x =>
                x.Select(
                    It.Is<IEnumerable<string>>(preferences => IsSequence(preferences, "de")),
                    null,
                    null
                )
            )
            .Returns(InteractiveLocales.German)
            .Verifiable();

        var locale = selector.Select(httpContext, settings: null);

        Assert.Same(InteractiveLocales.German, locale);
    }

    [Fact]
    public void Select_WhenTheRequestStatesNoPreference_SelectsWithAnEmptyList()
    {
        var selector = CreateSelector(InteractiveLocales.English);
        var settings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var httpContext = CreateHttpContext("?ui_locales=");
        MockCatalog
            .Setup(x =>
                x.Select(settings, It.Is<IEnumerable<string>>(preferences => !preferences.Any()))
            )
            .Returns(InteractiveLocales.English)
            .Verifiable();

        var locale = selector.Select(httpContext, settings);

        Assert.Same(InteractiveLocales.English, locale);
    }

    #endregion

    #region GetCarriedLanguageTag Tests

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("it", null)]
    [InlineData("FR", "fr")]
    public void GetCarriedLanguageTag_Always_KeepsOnlyARegisteredTag(
        string? uiLocales,
        string? expected
    )
    {
        var selector = CreateSelector(InteractiveLocales.English, InteractiveLocales.French);
        if (!string.IsNullOrEmpty(uiLocales))
        {
            IUiLocale? locale = expected is null ? null : InteractiveLocales.French;
            MockCatalog
                .Setup(x => x.TryGet(uiLocales, out locale))
                .Returns(expected is not null)
                .Verifiable();
        }

        var languageTag = selector.GetCarriedLanguageTag(uiLocales);

        Assert.Equal(expected, languageTag);
    }

    #endregion

    #region FormatUiLocalesQuery Tests

    [Fact]
    public void FormatUiLocalesQuery_WhenThereAreTags_EscapesThemAsOneParameter()
    {
        var query = InteractiveLocaleSelector.FormatUiLocalesQuery(["fr-CA", "de"]);

        Assert.Equal("&ui_locales=fr-CA%20de", query);
    }

    [Fact]
    public void FormatUiLocalesQuery_WhenThereAreNone_IsEmpty()
    {
        Assert.Equal(string.Empty, InteractiveLocaleSelector.FormatUiLocalesQuery(null));
        Assert.Equal(string.Empty, InteractiveLocaleSelector.FormatUiLocalesQuery([]));
    }

    #endregion
}
