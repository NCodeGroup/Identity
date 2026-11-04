// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Locales;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Selects the locale a page of the interactive surface renders in. The end-user's preferences are the request's
/// <c>ui_locales</c> (the authorization request's, carried on the redirect to the surface and on its forms) followed
/// by the user agent's <c>Accept-Language</c>, and the tenant's effective settings bound the choice through the
/// registered <see cref="IUiLocaleCatalog"/>. It also writes the <c>ui_locales</c> query a redirect to a page of the
/// surface carries.
/// </summary>
/// <remarks>
/// Every registered UI locale must be an <see cref="InteractiveLocale"/>, since the surface renders every locale the
/// catalog can select: the surface's options validation stops the host as it starts when one carries no messages,
/// and the selector refuses such a catalog when it is composed, so no page renders in the wrong language.
/// </remarks>
// See ADR-1402.
internal sealed class InteractiveLocaleSelector
{
    private const char PreferenceSeparator = ' ';
    private const char QueryStart = '?';
    private const char QuerySeparator = '&';
    private const string AnyLanguage = "*";

    /// <summary>
    /// Appends the <c>ui_locales</c> query that carries <paramref name="languageTags"/>, in their order, to
    /// <paramref name="url"/>, which may already have a query: <c>?ui_locales=fr%20de</c> or
    /// <c>&amp;ui_locales=fr%20de</c>. The URL is returned as it is when there is no tag to carry.
    /// </summary>
    /// <param name="url">The URL of a page of the surface.</param>
    /// <param name="languageTags">The preferred language tags, most preferred first, or <see langword="null"/>.</param>
    /// <returns>The URL with the query appended, or <paramref name="url"/> when there is no tag to carry.</returns>
    public static string AppendUiLocalesQuery(string url, IReadOnlyList<string>? languageTags) =>
        languageTags is null || languageTags.Count == 0
            ? url
            : AppendUiLocalesQuery(url, string.Join(PreferenceSeparator, languageTags));

    /// <summary>
    /// Appends the <c>ui_locales</c> query that carries <paramref name="languageTag"/> to <paramref name="url"/>,
    /// which may already have a query. The URL is returned as it is when there is no tag to carry.
    /// </summary>
    /// <param name="url">The URL of a page of the surface.</param>
    /// <param name="languageTag">The language tag to carry, or <see langword="null"/>.</param>
    /// <returns>The URL with the query appended, or <paramref name="url"/> when there is no tag to carry.</returns>
    public static string AppendUiLocalesQuery(string url, string? languageTag)
    {
        if (string.IsNullOrEmpty(languageTag))
        {
            return url;
        }

        var separator = url.Contains(QueryStart) ? QuerySeparator : QueryStart;
        return $"{url}{separator}{OpenIdProtocol.Parameters.UiLocales}="
            + Uri.EscapeDataString(languageTag);
    }

    // The ui_locales preferences come first, in their order; then the Accept-Language values by descending quality,
    // without the wildcard and the values a quality of zero rules out. The method runs on every page request, so it
    // splits the query over spans and orders the header by insertion, which keeps the header's order among equal
    // qualities, without LINQ.
    private static List<string> ReadPreferences(HttpRequest request)
    {
        List<string> preferences = [];

        foreach (var value in request.Query[OpenIdProtocol.Parameters.UiLocales])
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var span = value.AsSpan();
            foreach (var range in span.Split(PreferenceSeparator))
            {
                var languageTag = span[range].Trim();
                if (!languageTag.IsEmpty)
                {
                    preferences.Add(languageTag.ToString());
                }
            }
        }

        var acceptLanguage = request.GetTypedHeaders().AcceptLanguage;
        if (acceptLanguage.Count == 0)
        {
            return preferences;
        }

        List<StringWithQualityHeaderValue> ordered = new(acceptLanguage.Count);
        foreach (var header in acceptLanguage)
        {
            var languageTag = header.Value;
            if (
                header.Quality is 0.0
                || !languageTag.HasValue
                || languageTag.Equals(AnyLanguage, StringComparison.Ordinal)
            )
            {
                continue;
            }

            var quality = GetQuality(header);
            var index = ordered.Count;
            while (index > 0 && GetQuality(ordered[index - 1]) < quality)
            {
                index--;
            }

            ordered.Insert(index, header);
        }

        foreach (var header in ordered)
        {
            preferences.Add(header.Value.ToString());
        }

        return preferences;
    }

    // RFC 9110 §12.4.2: a value without a quality weight has a weight of 1.
    private static double GetQuality(StringWithQualityHeaderValue header) => header.Quality ?? 1.0;

    private IUiLocaleCatalog UiLocaleCatalog { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveLocaleSelector"/> class.
    /// </summary>
    /// <param name="uiLocaleCatalog">The registered UI locales, every one an <see cref="InteractiveLocale"/>.</param>
    /// <exception cref="InvalidOperationException">A registered UI locale carries no interactive messages.</exception>
    public InteractiveLocaleSelector(IUiLocaleCatalog uiLocaleCatalog)
    {
        var failures = InteractiveAuthenticationOptionsValidator.FindLocalesWithoutMessages(
            uiLocaleCatalog
        );
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join(PreferenceSeparator, failures));
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

        // The constructor refuses a catalog with any other kind of locale, and the validator stops the host first.
        return (InteractiveLocale)locale;
    }

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
}
