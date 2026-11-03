// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Locales;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

/// <summary>
/// A registered UI locale that carries no interactive messages, as a host that renders an interface of its own
/// registers one: the surface's validator and selector refuse it.
/// </summary>
/// <param name="LanguageTag">The BCP 47 language tag of the locale.</param>
internal sealed record PlainLocale(string LanguageTag) : IUiLocale;
