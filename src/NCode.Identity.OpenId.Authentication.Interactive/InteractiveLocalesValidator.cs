// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Locales;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Checks when the host starts that every registered UI locale is an <see cref="InteractiveLocale"/>, since the
/// surface renders every locale the catalog can select: a locale registered without the surface's messages stops
/// the host with an error naming its tag, never serves a page in the wrong language, and never waits for the first
/// request to be found. The check runs as a validation of <see cref="InteractiveAuthenticationOptions"/>, which the
/// surface's registration validates on start.
/// </summary>
// See ADR-1402.
internal sealed class InteractiveLocalesValidator(IUiLocaleCatalog uiLocaleCatalog)
    : IValidateOptions<InteractiveAuthenticationOptions>
{
    private IUiLocaleCatalog UiLocaleCatalog { get; } = uiLocaleCatalog;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, InteractiveAuthenticationOptions options)
    {
        var failures = FindLocalesWithoutMessages(UiLocaleCatalog);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Describes every registered locale that carries no interactive messages, one failure per locale.
    /// </summary>
    /// <param name="uiLocaleCatalog">The registered UI locales.</param>
    /// <returns>The failures, empty when every registered locale is an <see cref="InteractiveLocale"/>.</returns>
    public static IReadOnlyList<string> FindLocalesWithoutMessages(IUiLocaleCatalog uiLocaleCatalog)
    {
        List<string> failures = [];
        foreach (var locale in uiLocaleCatalog.Locales)
        {
            if (locale is not InteractiveLocale)
            {
                failures.Add(
                    $"The UI locale '{locale.LanguageTag}' carries no interactive messages; register it as an "
                        + $"{nameof(InteractiveLocale)}."
                );
            }
        }

        return failures;
    }
}
