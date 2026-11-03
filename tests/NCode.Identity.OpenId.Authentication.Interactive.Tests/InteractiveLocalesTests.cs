// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

/// <summary>
/// The locales the surface ships: the data <see cref="InteractiveLocales"/> declares, which the renderer tests leave
/// to this class by rendering locales of their own.
/// </summary>
public class InteractiveLocalesTests
{
    #region All Tests

    [Fact]
    public void All_Always_ListsEnglishFirstThenGermanSpanishAndFrench()
    {
        Assert.Collection(
            InteractiveLocales.All,
            locale => Assert.Same(InteractiveLocales.English, locale),
            locale => Assert.Same(InteractiveLocales.German, locale),
            locale => Assert.Same(InteractiveLocales.Spanish, locale),
            locale => Assert.Same(InteractiveLocales.French, locale)
        );
        Assert.Equal(
            ["en", "de", "es", "fr"],
            InteractiveLocales.All.Select(locale => locale.LanguageTag)
        );
    }

    #endregion

    #region Locale Tests

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    public void Locale_Always_CarriesEveryMessageAndPlacesTheApplicationInItsConsentPrompt(
        string languageTag
    )
    {
        var locale = Assert.Single(
            InteractiveLocales.All,
            candidate => candidate.LanguageTag == languageTag
        );
        var messages = locale.Messages;

        string[] values =
        [
            messages.SignInTitle,
            messages.UsernameLabel,
            messages.PasswordLabel,
            messages.SignInButton,
            messages.AuthorizeAccessTitle,
            messages.ConsentPromptFormat,
            messages.DenyButton,
            messages.AllowButton,
            messages.SignOutTitle,
            messages.SignOutQuestion,
            messages.StaySignedInButton,
            messages.SignOutButton,
            messages.AuthorizationErrorTitle,
            messages.InvalidCredentialsError,
            messages.MissingCredentialsError,
            messages.AccessDeniedError,
            messages.SignInFailedError,
        ];
        Assert.All(values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Contains("{0}", messages.ConsentPromptFormat, StringComparison.Ordinal);
    }

    [Fact]
    public void Locale_Always_DescribesEachLoginErrorDifferently()
    {
        // The login page maps each error code to one of these, so a reader can tell the codes apart.
        Assert.All(
            InteractiveLocales.All,
            locale =>
            {
                var messages = locale.Messages;
                string[] errors =
                [
                    messages.InvalidCredentialsError,
                    messages.MissingCredentialsError,
                    messages.AccessDeniedError,
                    messages.SignInFailedError,
                ];
                Assert.Equal(errors.Length, errors.Distinct(StringComparer.Ordinal).Count());
            }
        );
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    public void Locale_Always_HasItsWordingInTheBundlesCatalog(string languageTag)
    {
        // The bundle draws the same page over the one the server rendered, so a change to a page's wording in a
        // shipped locale is made on both sides; the bundle's catalog source is read from the build output, where the
        // project links it. A sentence that places the application's name is a template literal in the catalog.
        var locale = Assert.Single(
            InteractiveLocales.All,
            candidate => candidate.LanguageTag == languageTag
        );
        var catalog = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "auth-i18n", $"{languageTag}.ts"),
            SecureEncoding.UTF8
        );
        var messages = locale.Messages;

        string[] expected =
        [
            messages.SignInTitle,
            messages.UsernameLabel,
            messages.PasswordLabel,
            messages.SignInButton,
            messages.AuthorizeAccessTitle,
            string.Format(CultureInfo.InvariantCulture, messages.ConsentPromptFormat, "${appName}"),
            messages.DenyButton,
            messages.AllowButton,
            messages.SignOutTitle,
            messages.SignOutQuestion,
            messages.StaySignedInButton,
            messages.SignOutButton,
            messages.AuthorizationErrorTitle,
            messages.InvalidCredentialsError,
            messages.MissingCredentialsError,
            messages.AccessDeniedError,
            messages.SignInFailedError,
        ];
        Assert.All(expected, text => Assert.Contains(text, catalog, StringComparison.Ordinal));
    }

    #endregion
}
