// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Locales;

/// <summary>
/// Provides the default implementation of <see cref="IUiLocaleCatalog"/> over the registered <see cref="IUiLocale"/>
/// strategy collection. Locales are keyed by their language tag without regard to case.
/// </summary>
/// <remarks>
/// The registered set is validated when the catalog is composed: a locale whose tag is not a well-formed BCP 47
/// language tag, or a tag registered more than once, is an error, never ignored, and one error names every such tag.
/// The catalog reads only the registered locales, so the setting descriptors that name its domain can consult it
/// inside options validation.
/// </remarks>
// See ADR-1402.
internal sealed partial class DefaultUiLocaleCatalog : IUiLocaleCatalog
{
    private const char SubtagSeparator = '-';
    private const char FailureSeparator = ' ';

    // A language subtag of two to eight letters, then any number of subtags of one to eight letters or digits (BCP 47
    // §2.1), which keeps a tag safe to write into an HTML attribute and a URL.
    [GeneratedRegex("^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex LanguageTagRegex();

    // Every malformed or duplicate tag is reported in the one error, so a host with several misregistrations fixes
    // them in one restart.
    private static FrozenDictionary<string, IUiLocale> Index(ImmutableArray<IUiLocale> locales)
    {
        var localesByTag = new Dictionary<string, IUiLocale>(StringComparer.OrdinalIgnoreCase);
        List<string> failures = [];
        foreach (var locale in locales)
        {
            var languageTag = locale.LanguageTag;
            if (string.IsNullOrEmpty(languageTag) || !LanguageTagRegex().IsMatch(languageTag))
            {
                failures.Add(
                    $"The UI locale '{languageTag}' is not a well-formed BCP 47 language tag."
                );
            }
            else if (!localesByTag.TryAdd(languageTag, locale))
            {
                failures.Add($"The UI locale '{languageTag}' is registered more than once.");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join(FailureSeparator, failures));
        }

        return localesByTag.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    // An unset ui_locales_supported lets every registered locale be selected (ADR-1010).
    private static bool IsSupported(
        IUiLocale locale,
        IReadOnlyCollection<string>? supportedLanguageTags
    )
    {
        if (supportedLanguageTags is null)
        {
            return true;
        }

        foreach (var supportedLanguageTag in supportedLanguageTags)
        {
            if (
                string.Equals(
                    supportedLanguageTag,
                    locale.LanguageTag,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<IUiLocale> Locales => LocaleList;

    private ImmutableArray<IUiLocale> LocaleList { get; }

    private FrozenDictionary<string, IUiLocale> LocalesByTag { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUiLocaleCatalog"/> class.
    /// </summary>
    /// <param name="locales">The registered UI locales, in their registration order.</param>
    /// <exception cref="InvalidOperationException">A registered locale's tag is not a well-formed BCP 47 language
    /// tag, or a tag is registered more than once; the message names every such tag.</exception>
    public DefaultUiLocaleCatalog(IEnumerable<IUiLocale> locales)
    {
        // The injected sequence is enumerated once, so the list and the index always hold the same locales.
        LocaleList = [.. locales];
        LocalesByTag = Index(LocaleList);
    }

    /// <inheritdoc />
    public bool TryGet(string languageTag, [MaybeNullWhen(false)] out IUiLocale locale)
    {
        if (string.IsNullOrEmpty(languageTag))
        {
            locale = null;
            return false;
        }

        return LocalesByTag.TryGetValue(languageTag, out locale);
    }

    /// <inheritdoc />
    public IUiLocale Select(
        IReadOnlySettingCollection settings,
        IEnumerable<string> preferredLanguageTags
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(preferredLanguageTags);

        var supportedLanguageTags = settings.TryGetValue(
            OpenIdSettingKeys.UiLocalesSupported,
            out var supported
        )
            ? supported
            : null;
        var defaultLanguageTag = settings.TryGetValue(
            OpenIdSettingKeys.DefaultUiLocale,
            out var defaultLocale
        )
            ? defaultLocale
            : null;

        return Select(preferredLanguageTags, supportedLanguageTags, defaultLanguageTag);
    }

    /// <inheritdoc />
    public IUiLocale Select(
        IEnumerable<string> preferredLanguageTags,
        IReadOnlyCollection<string>? supportedLanguageTags,
        string? defaultLanguageTag
    )
    {
        ArgumentNullException.ThrowIfNull(preferredLanguageTags);

        if (LocaleList.Length == 0)
        {
            throw new InvalidOperationException("No UI locale is registered.");
        }

        foreach (var preferredLanguageTag in preferredLanguageTags)
        {
            if (
                TryLookup(preferredLanguageTag, out var candidate)
                && IsSupported(candidate, supportedLanguageTags)
            )
            {
                return candidate;
            }
        }

        if (
            !string.IsNullOrEmpty(defaultLanguageTag)
            && TryGet(defaultLanguageTag, out var defaultLocale)
            && IsSupported(defaultLocale, supportedLanguageTags)
        )
        {
            return defaultLocale;
        }

        // The ceiling bounds the fallback too, so the page renders in a locale the tenant advertises whenever the
        // ceiling lists a registered one; a ceiling that lists none leaves only the first registered locale.
        foreach (var locale in LocaleList)
        {
            if (IsSupported(locale, supportedLanguageTags))
            {
                return locale;
            }
        }

        return LocaleList[0];
    }

    // RFC 4647 §3.4 lookup: the tag as written, then the tag with its last subtag removed, until a registered locale
    // matches.
    private bool TryLookup(string languageTag, [MaybeNullWhen(false)] out IUiLocale locale)
    {
        var lookup = LocalesByTag.GetAlternateLookup<ReadOnlySpan<char>>();
        var candidate = languageTag.AsSpan().Trim();
        while (!candidate.IsEmpty)
        {
            if (lookup.TryGetValue(candidate, out locale))
            {
                return true;
            }

            var separatorIndex = candidate.LastIndexOf(SubtagSeparator);
            if (separatorIndex < 0)
            {
                break;
            }

            candidate = candidate[..separatorIndex];
        }

        locale = null;
        return false;
    }
}
