// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Locales;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Locales;

public class DefaultUiLocaleCatalogTests : BaseTests
{
    private static DefaultUiLocaleCatalog CreateCatalog(params string[] languageTags) =>
        new(languageTags.Select(IUiLocale (tag) => new FakeLocale(tag)));

    private IReadOnlySettingCollection CreateSettings(
        IReadOnlyCollection<string>? supported,
        string? defaultLanguageTag
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var supportedValue = supported;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.UiLocalesSupported, out supportedValue))
            .Returns(supported is not null)
            .Verifiable();
        var defaultValue = defaultLanguageTag;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.DefaultUiLocale, out defaultValue))
            .Returns(defaultLanguageTag is not null)
            .Verifiable();
        return mockSettings.Object;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenSeveralTagsAreBad_NamesEveryOneInTheOneError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog("en", "e", "EN", "fr CA", "fr")
        );

        Assert.Equal(
            "The UI locale 'e' is not a well-formed BCP 47 language tag. "
                + "The UI locale 'EN' is registered more than once. "
                + "The UI locale 'fr CA' is not a well-formed BCP 47 language tag.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenATagIsRegisteredTwiceInAnyCase_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog("en", "fr", "FR")
        );

        Assert.Contains("'FR'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("en_US")]
    [InlineData("en-")]
    [InlineData("fr CA")]
    [InlineData("<en>")]
    [InlineData("en-123456789")]
    public void Constructor_WhenATagIsNotWellFormed_Throws(string languageTag)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateCatalog(languageTag));

        Assert.Contains($"'{languageTag}'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Locales Tests

    [Fact]
    public void Locales_Always_ListsTheRegisteredLocalesInRegistrationOrder()
    {
        var catalog = CreateCatalog("en", "fr-CA", "de");

        Assert.Equal(["en", "fr-CA", "de"], catalog.Locales.Select(locale => locale.LanguageTag));
    }

    #endregion

    #region TryGet Tests

    [Theory]
    [InlineData("fr", true)]
    [InlineData("FR", true)]
    [InlineData("fr-CA", false)]
    [InlineData("it", false)]
    [InlineData("", false)]
    public void TryGet_Always_MatchesTheWholeTagWithoutRegardToCase(
        string languageTag,
        bool expected
    )
    {
        var catalog = CreateCatalog("en", "fr");

        var found = catalog.TryGet(languageTag, out var locale);

        Assert.Equal(expected, found);
        Assert.Equal(expected ? "fr" : null, locale?.LanguageTag);
    }

    #endregion

    #region Select Tests

    [Fact]
    public void Select_WhenNoLocaleIsRegistered_Throws()
    {
        var catalog = CreateCatalog();

        Assert.Throws<InvalidOperationException>(() => catalog.Select(["en"], null, null));
    }

    [Theory]
    [InlineData(new[] { "fr" }, "fr")]
    [InlineData(new[] { "FR" }, "fr")]
    [InlineData(new[] { "fr-CA" }, "fr")]
    [InlineData(new[] { "fr-Latn-CA" }, "fr")]
    [InlineData(new[] { "it", "de", "fr" }, "de")]
    [InlineData(new[] { "it" }, "en")]
    [InlineData(new[] { "" }, "en")]
    [InlineData(new string[0], "en")]
    public void Select_WhenEveryRegisteredLocaleIsSupported_PicksTheFirstPreferenceItServes(
        string[] preferences,
        string expected
    )
    {
        var catalog = CreateCatalog("en", "de", "fr");

        var locale = catalog.Select(preferences, null, null);

        Assert.Equal(expected, locale.LanguageTag);
    }

    [Fact]
    public void Select_WhenTheTenantSupportsSomeLocales_SkipsAPreferenceOutsideThem()
    {
        var catalog = CreateCatalog("en", "de", "fr");

        var locale = catalog.Select(["de", "fr"], ["en", "FR"], null);

        Assert.Equal("fr", locale.LanguageTag);
    }

    [Fact]
    public void Select_WhenNoPreferenceIsServed_FallsBackToTheDefaultLocale()
    {
        var catalog = CreateCatalog("en", "de", "fr");

        var locale = catalog.Select(["it"], ["en", "fr"], "fr");

        Assert.Equal("fr", locale.LanguageTag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("it")]
    public void Select_WhenTheDefaultLocaleIsUnsetOrUnregistered_FallsBackToTheFirstRegistered(
        string? defaultLanguageTag
    )
    {
        var catalog = CreateCatalog("de", "en");

        var locale = catalog.Select(["it"], null, defaultLanguageTag);

        Assert.Equal("de", locale.LanguageTag);
    }

    [Fact]
    public void Select_WhenTheDefaultLocaleIsOutsideTheSupportedSet_FallsBackToTheFirstRegisteredInIt()
    {
        var catalog = CreateCatalog("en", "de", "fr");

        var locale = catalog.Select(["it"], ["FR", "de"], "en");

        Assert.Equal("de", locale.LanguageTag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Select_WhenNoDefaultIsSetAndTheSupportedSetOmitsTheFirstRegistered_FallsBackToTheFirstRegisteredInIt(
        string? defaultLanguageTag
    )
    {
        var catalog = CreateCatalog("en", "de", "fr");

        var locale = catalog.Select([], ["fr"], defaultLanguageTag);

        Assert.Equal("fr", locale.LanguageTag);
    }

    [Theory]
    [InlineData(new object[] { new string[0] })]
    [InlineData(new object[] { new[] { "it" } })]
    public void Select_WhenTheSupportedSetHoldsNoRegisteredLocale_FallsBackToTheFirstRegistered(
        string[] supported
    )
    {
        var catalog = CreateCatalog("en", "de");

        var locale = catalog.Select(["de"], supported, "de");

        Assert.Equal("en", locale.LanguageTag);
    }

    [Fact]
    public void Select_WithSettings_ReadsTheSupportedAndDefaultLocalesFromThem()
    {
        var catalog = CreateCatalog("en", "de", "fr");
        var settings = CreateSettings(["en", "de"], "de");

        var locale = catalog.Select(settings, ["fr", "it"]);

        Assert.Equal("de", locale.LanguageTag);
    }

    [Fact]
    public void Select_WithSettingsThatSetNeither_LetsEveryRegisteredLocaleBeSelected()
    {
        var catalog = CreateCatalog("en", "de", "fr");
        var settings = CreateSettings(null, null);

        var locale = catalog.Select(settings, ["fr"]);

        Assert.Equal("fr", locale.LanguageTag);
    }

    #endregion

    private sealed record FakeLocale(string LanguageTag) : IUiLocale;
}
