// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Locales;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// A locale the interactive surface renders: the BCP 47 language tag it is selected and advertised by, and the text of
/// the pages the surface renders on the server. The surface registers one as an <see cref="IUiLocale"/> for every
/// locale it ships, and a host adds a locale, or replaces a shipped one by its tag, through
/// <c>AddInteractiveLocale</c>; the <c>apps/auth</c> bundle renders the locales its own catalog holds, in its
/// default language for any other.
/// </summary>
/// <param name="LanguageTag">The BCP 47 language tag of the locale, such as <c>fr</c>.</param>
/// <param name="Messages">The text of the server-rendered pages in the locale.</param>
// See ADR-1402.
[PublicAPI]
public sealed record InteractiveLocale(string LanguageTag, InteractiveMessages Messages)
    : IUiLocale;
