// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Navigation;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// The <see cref="IBffFrontendSelector"/> named <c>path</c>, which selects a frontend by a local path prefix of the
/// request. Prefixes are compared at segment boundaries and ignore case, like routing.
/// </summary>
internal sealed class PathBffFrontendSelector : IBffFrontendSelector
{
    /// <inheritdoc />
    public string Name => BffFrontendSelectorNames.Path;

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(BffFrontendSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Values.Count == 0)
            return ["Values must name at least one path."];

        List<string> failures = [];
        List<string> prefixes = [];
        foreach (var value in selection.Values)
        {
            if (IsValidPrefix(value))
            {
                prefixes.Add(value);
                continue;
            }

            failures.Add(
                $"Values entry '{value}' must be a well-formed local path that starts with '/' and does not end with '/'."
            );
        }

        foreach (var path in selection.Paths)
        {
            if (!prefixes.Exists(x => Covers(x, path)))
            {
                failures.Add(
                    $"'{path}' must be under one of the Values, so its requests select the frontend."
                );
            }
        }

        return failures;
    }

    /// <inheritdoc />
    public bool Overlaps(BffFrontendSelection first, BffFrontendSelection second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        foreach (var firstValue in first.Values)
        {
            foreach (var secondValue in second.Values)
            {
                if (Covers(firstValue, secondValue) || Covers(secondValue, firstValue))
                    return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsMatch(HttpRequest request, BffFrontendSelection selection)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selection);

        foreach (var value in selection.Values)
        {
            if (
                IsLocalPath(value)
                && request.Path.StartsWithSegments(value, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidPrefix(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length > 1
        && value[^1] != '/'
        && !value.Contains('?', StringComparison.Ordinal)
        && BffReturnUrls.IsWellFormedPath(value);

    private static bool IsLocalPath(string? value) =>
        !string.IsNullOrEmpty(value) && value[0] == '/';

    private static bool Covers(string? prefix, string? path) =>
        IsLocalPath(prefix)
        && IsLocalPath(path)
        && new PathString(path).StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase);
}
