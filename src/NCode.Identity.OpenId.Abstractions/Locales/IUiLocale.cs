// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Locales;

/// <summary>
/// A locale that the end-user interface of the OpenID Provider renders, named by its BCP 47 language tag. The package
/// that renders the interface registers one for every locale it renders, and the registered set is the vocabulary of
/// the <c>ui_locales_supported</c> and <c>default_ui_locale</c> settings and of the <c>ui_locales</c> authorization
/// parameter.
/// </summary>
// See ADR-1402.
[PublicAPI]
public interface IUiLocale
{
    /// <summary>
    /// Gets the BCP 47 language tag of the locale, such as <c>en</c> or <c>fr-CA</c>.
    /// </summary>
    string LanguageTag { get; }
}
