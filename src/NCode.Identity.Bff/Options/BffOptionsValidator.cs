// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Validates <see cref="BffOptions"/> when the host starts, so a setting that cannot work stops the host instead of
/// failing on the first request.
/// </summary>
internal class BffOptionsValidator(
    IHostEnvironment environment,
    IEnumerable<IBffFrontendSelector> selectors
) : IValidateOptions<BffOptions>
{
    private const string Key = "Bff";

    private static readonly string[] ForbiddenHeaderNames =
    [
        "Accept",
        "Accept-Language",
        "Content-Language",
        "Content-Type",
        "Range",
        "Cookie",
        "Authorization",
        "DPoP",
    ];

    private static void ValidateTokens(BffTokenOptions? tokens, List<string> failures)
    {
        if (tokens is null)
        {
            failures.Add("Bff:Tokens must be set.");
            return;
        }

        if (
            tokens.RefreshBeforeExpiry < TimeSpan.Zero
            || tokens.RefreshBeforeExpiry > TimeSpan.FromHours(1)
        )
        {
            failures.Add("Bff:Tokens:RefreshBeforeExpiry must be between 0 and 1 hour.");
        }

        if (
            tokens.RefreshLeaseDuration < TimeSpan.FromSeconds(10)
            || tokens.RefreshLeaseDuration > TimeSpan.FromMinutes(5)
        )
        {
            failures.Add(
                "Bff:Tokens:RefreshLeaseDuration must be between 10 seconds and 5 minutes."
            );
        }
    }

    private static void ValidateAntiforgery(
        BffAntiforgeryOptions? antiforgery,
        List<string> failures
    )
    {
        if (antiforgery is null)
        {
            failures.Add("Bff:Antiforgery must be set.");
            return;
        }

        if (string.IsNullOrWhiteSpace(antiforgery.HeaderName))
        {
            failures.Add("Bff:Antiforgery:HeaderName must not be blank.");
        }
        else if (
            ForbiddenHeaderNames.Any(x =>
                string.Equals(x, antiforgery.HeaderName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            failures.Add(
                $"Bff:Antiforgery:HeaderName '{antiforgery.HeaderName}' must not be a header that skips a CORS preflight or carries credentials."
            );
        }

        if (string.IsNullOrWhiteSpace(antiforgery.HeaderValue))
        {
            failures.Add("Bff:Antiforgery:HeaderValue must not be blank.");
        }
    }

    private ILookup<string, IBffFrontendSelector> Selectors { get; } =
        selectors.ToLookup(x => x.Name, StringComparer.Ordinal);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var isDevelopment = environment.IsDevelopment();

        ValidateSelectors(failures);
        ValidateFrontends(options.Frontends, isDevelopment, failures);
        ValidateAntiforgery(options.Antiforgery, failures);
        ValidateTokens(options.Tokens, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateSelectors(List<string> failures)
    {
        foreach (var group in Selectors)
        {
            if (group.Count() > 1)
            {
                failures.Add(
                    $"The frontend selection rule '{group.Key}' is registered more than once."
                );
            }
        }
    }

    private void ValidateFrontends(
        Dictionary<string, BffFrontendOptions>? frontends,
        bool isDevelopment,
        List<string> failures
    )
    {
        if (frontends is null)
        {
            failures.Add($"{Key}:Frontends must be set.");
            return;
        }

        if (frontends.Count == 0)
        {
            failures.Add($"{Key}:Frontends must configure at least one frontend.");
        }

        var ruled =
            new List<(
                string Name,
                IBffFrontendSelector Selector,
                BffFrontendSelection Selection
            )>();
        var fallbackCount = 0;
        var origins = new List<(string Name, string Origin)>();

        foreach (var (name, frontend) in frontends.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!BffFrontendNames.IsValid(name))
            {
                failures.Add(
                    $"{Key}:Frontends key '{name}' must be 1 to 63 lowercase letters, digits and hyphens that start and end with a letter or digit."
                );
            }

            var frontendKey = $"{Key}:Frontends:{name}";
            BffFrontendOptionsValidation.Validate(frontendKey, frontend, isDevelopment, failures);

            if (frontend is null)
                continue;

            origins.Add((name, $"{frontend.PublicOrigin}{frontend.PathPrefix}"));

            var rule = frontend.Selection?.Rule;
            if (string.IsNullOrWhiteSpace(rule))
            {
                fallbackCount++;
                continue;
            }

            var selector = Selectors[rule].FirstOrDefault();
            if (selector is null)
            {
                failures.Add(
                    $"{frontendKey}:Selection:Rule '{rule}' is not a registered frontend selection rule."
                );
                continue;
            }

            if (frontend.RemoteApis is null)
                continue;

            var selection = BffFrontend.CreateSelection(frontend);
            if (selection is null)
                continue;

            foreach (var failure in selector.Validate(selection))
            {
                failures.Add($"{frontendKey}:Selection:{failure}");
            }

            ruled.Add((name, selector, selection));
        }

        if (fallbackCount > 1)
        {
            failures.Add(
                $"At most one entry of {Key}:Frontends may have no Selection:Rule; it serves the requests no other frontend selects."
            );
        }

        for (var i = 0; i < ruled.Count; i++)
        {
            for (var j = i + 1; j < ruled.Count; j++)
            {
                var first = ruled[i];
                var second = ruled[j];
                if (
                    ReferenceEquals(first.Selector, second.Selector)
                    && first.Selector.Overlaps(first.Selection, second.Selection)
                )
                {
                    failures.Add(
                        $"{Key}:Frontends:{first.Name}:Selection and {Key}:Frontends:{second.Name}:Selection select the same requests."
                    );
                }
            }
        }

        for (var i = 0; i < origins.Count; i++)
        {
            for (var j = i + 1; j < origins.Count; j++)
            {
                if (!string.Equals(origins[i].Origin, origins[j].Origin, StringComparison.Ordinal))
                    continue;

                failures.Add(
                    $"{Key}:Frontends:{origins[i].Name} and {Key}:Frontends:{origins[j].Name} have the same PublicOrigin and PathPrefix; each frontend needs its own callback URI."
                );
            }
        }
    }
}
