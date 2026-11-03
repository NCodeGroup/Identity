// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Locales;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Selects the locale a page of the interactive surface renders in. The end-user's preferences are the request's
/// <c>ui_locales</c> (the authorization request's, carried on the redirect to the surface and on its forms) followed
/// by the user agent's <c>Accept-Language</c>, and the tenant's effective settings bound the choice through the
/// registered <see cref="IUiLocaleCatalog"/>.
/// </summary>
/// <remarks>
/// Every registered UI locale must be an <see cref="InteractiveLocale"/>, since the surface renders every locale the
/// catalog can select: a locale registered without the surface's messages is an error when the selector is composed,
/// never a page in the wrong language.
/// </remarks>
// See ADR-1402.
internal sealed class InteractiveLocaleSelector
{
    private const char PreferenceSeparator = ' ';

    private IUiLocaleCatalog UiLocaleCatalog { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveLocaleSelector"/> class.
    /// </summary>
    /// <param name="uiLocaleCatalog">The registered UI locales, every one an <see cref="InteractiveLocale"/>.</param>
    /// <exception cref="InvalidOperationException">A registered UI locale carries no interactive messages.</exception>
    public InteractiveLocaleSelector(IUiLocaleCatalog uiLocaleCatalog)
    {
        foreach (var locale in uiLocaleCatalog.Locales)
        {
            if (locale is not InteractiveLocale)
            {
                throw new InvalidOperationException(
                    $"The UI locale '{locale.LanguageTag}' carries no interactive messages; register it as an "
                        + $"{nameof(InteractiveLocale)}."
                );
            }
        }

        UiLocaleCatalog = uiLocaleCatalog;
    }

    /// <summary>
    /// Selects the locale the page of <paramref name="httpContext"/> renders in for the tenant whose effective
    /// settings are <paramref name="settings"/>, or for no tenant when they are <see langword="null"/>.
    /// </summary>
    /// <param name="httpContext">The request of the page.</param>
    /// <param name="settings">The tenant's effective settings, or <see langword="null"/> when the request resolved no
    /// tenant.</param>
    /// <returns>The selected locale.</returns>
    public InteractiveLocale Select(HttpContext httpContext, IReadOnlySettingCollection? settings)
    {
        var preferences = ReadPreferences(httpContext.Request);
        var locale = settings is null
            ? UiLocaleCatalog.Select(preferences, null, null)
            : UiLocaleCatalog.Select(settings, preferences);

        // The constructor refuses a catalog with any other kind of locale.
        return (InteractiveLocale)locale;
    }

    /// <summary>
    /// Formats the query suffix that carries <paramref name="languageTags"/> to a page of the surface as its
    /// <c>ui_locales</c>, in their order: <c>&amp;ui_locales=fr%20de</c>, or empty when there is none to carry.
    /// </summary>
    /// <param name="languageTags">The preferred language tags, most preferred first, or <see langword="null"/>.</param>
    /// <returns>The query suffix, which starts with <c>&amp;</c>, or an empty string.</returns>
    public static string FormatUiLocalesQuery(IReadOnlyList<string>? languageTags) =>
        languageTags is null || languageTags.Count == 0
            ? string.Empty
            : $"&{OpenIdProtocol.Parameters.UiLocales}="
                + Uri.EscapeDataString(string.Join(PreferenceSeparator, languageTags));

    /// <summary>
    /// Returns the language tag a form or a redirect carries as its <c>ui_locales</c>, when it names a registered
    /// locale, so a page the surface redirects back to renders in the same locale; any other value is dropped.
    /// </summary>
    /// <param name="uiLocales">The carried <c>ui_locales</c> value, or <see langword="null"/>.</param>
    /// <returns>The registered locale's language tag, or <see langword="null"/>.</returns>
    public string? GetCarriedLanguageTag(string? uiLocales) =>
        !string.IsNullOrEmpty(uiLocales) && UiLocaleCatalog.TryGet(uiLocales, out var locale)
            ? locale.LanguageTag
            : null;

    // The ui_locales preferences come first, in their order; then the Accept-Language values by descending quality,
    // without the wildcard and the values a quality of zero rules out.
    private static List<string> ReadPreferences(HttpRequest request)
    {
        List<string> preferences = [];

        foreach (var value in request.Query[OpenIdProtocol.Parameters.UiLocales])
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            preferences.AddRange(
                value.Split(
                    PreferenceSeparator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            );
        }

        var acceptLanguage = request.GetTypedHeaders().AcceptLanguage;
        if (acceptLanguage.Count == 0)
        {
            return preferences;
        }

        foreach (
            var header in acceptLanguage
                .Where(header => header.Quality is not 0.0)
                .OrderByDescending(header => header.Quality ?? 1.0)
        )
        {
            var languageTag = header.Value;
            if (languageTag.HasValue && !languageTag.Equals("*", StringComparison.Ordinal))
            {
                preferences.Add(languageTag.ToString());
            }
        }

        return preferences;
    }
}
