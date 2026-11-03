// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Locales;

/// <summary>
/// The registry of the <see cref="IUiLocale"/> implementations the OpenID Provider's end-user interface renders. It
/// resolves a locale by its language tag and selects the locale a page renders in from the end-user's preferences and
/// a tenant's effective settings.
/// </summary>
// See ADR-1402.
[PublicAPI]
public interface IUiLocaleCatalog
{
    /// <summary>
    /// Gets the registered locales, in registration order. The first one is the locale a tenant that sets no
    /// <c>default_ui_locale</c> renders in when no preference selects another.
    /// </summary>
    IReadOnlyList<IUiLocale> Locales { get; }

    /// <summary>
    /// Attempts to get the registered locale whose language tag is <paramref name="languageTag"/>, compared without
    /// regard to case, as BCP 47 tags are.
    /// </summary>
    /// <param name="languageTag">The language tag to look up, such as <c>fr</c>.</param>
    /// <param name="locale">When this method returns, contains the matching locale, if one is registered.</param>
    /// <returns><c>true</c> if a locale is registered for the tag; otherwise, <c>false</c>.</returns>
    bool TryGet(string languageTag, [MaybeNullWhen(false)] out IUiLocale locale);

    /// <summary>
    /// Selects the locale a page renders in for a tenant: the first of <paramref name="preferredLanguageTags"/> that
    /// a registered locale serves and the tenant's effective <c>ui_locales_supported</c> lists, otherwise the tenant's
    /// effective <c>default_ui_locale</c>, otherwise the first registered locale.
    /// </summary>
    /// <remarks>
    /// A preferred tag selects a registered locale by lookup (RFC 4647 §3.4): the tag itself, then the tag with its
    /// last subtag removed, until a registered locale matches, so <c>fr-CA</c> selects a registered <c>fr</c>. A
    /// tenant whose <c>ui_locales_supported</c> is unset lets every registered locale be selected. A preference that
    /// selects nothing is a preference the end-user does not get, never an error.
    /// </remarks>
    /// <param name="settings">The tenant's effective settings.</param>
    /// <param name="preferredLanguageTags">The end-user's preferred language tags, most preferred first.</param>
    /// <returns>The selected locale.</returns>
    /// <exception cref="InvalidOperationException">No locale is registered.</exception>
    IUiLocale Select(
        IReadOnlySettingCollection settings,
        IEnumerable<string> preferredLanguageTags
    );

    /// <summary>
    /// Selects the locale a page renders in from explicit bounds: the first of <paramref name="preferredLanguageTags"/>
    /// that a registered locale serves and <paramref name="supportedLanguageTags"/> lists, otherwise the registered
    /// locale named by <paramref name="defaultLanguageTag"/>, otherwise the first registered locale.
    /// </summary>
    /// <param name="preferredLanguageTags">The end-user's preferred language tags, most preferred first.</param>
    /// <param name="supportedLanguageTags">The language tags a preference may select, or <see langword="null"/> when
    /// every registered locale may be selected.</param>
    /// <param name="defaultLanguageTag">The language tag of the locale to fall back to, or <see langword="null"/> or
    /// empty to fall back to the first registered locale.</param>
    /// <returns>The selected locale.</returns>
    /// <exception cref="InvalidOperationException">No locale is registered.</exception>
    IUiLocale Select(
        IEnumerable<string> preferredLanguageTags,
        IReadOnlyCollection<string>? supportedLanguageTags,
        string? defaultLanguageTag
    );
}
