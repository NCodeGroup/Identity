// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

    #endregion
}
