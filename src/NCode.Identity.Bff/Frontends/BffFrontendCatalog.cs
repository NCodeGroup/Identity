// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Provides the frontends that the BFF serves and selects the frontend of a request.
/// </summary>
/// <remarks>
/// <para>
/// The frontends are built once, on first access, so resolving a service that depends on the catalog never reads or
/// validates the options.
/// </para>
/// <para>
/// A frontend that names a selection rule is selected by that rule. The frontend that names no rule serves every
/// request that no other frontend selects. A request that two frontends select fails closed.
/// </para>
/// </remarks>
internal sealed class BffFrontendCatalog(
    IOptions<BffOptions> options,
    IEnumerable<IBffFrontendSelector> selectors
)
{
    private static List<BffFrontend> Create(
        BffOptions bffOptions,
        IEnumerable<IBffFrontendSelector> selectors
    )
    {
        var selectorsByName = new Dictionary<string, IBffFrontendSelector>(StringComparer.Ordinal);
        foreach (var selector in selectors)
        {
            if (!selectorsByName.TryAdd(selector.Name, selector))
                throw new InvalidOperationException(
                    $"The frontend selection rule '{selector.Name}' is registered more than once."
                );
        }

        var frontends = new List<BffFrontend>();
        foreach (
            var (name, frontendOptions) in bffOptions.Frontends.OrderBy(
                x => x.Key,
                StringComparer.Ordinal
            )
        )
        {
            IBffFrontendSelector? selector = null;
            var rule = frontendOptions.Selection?.Rule;
            if (
                !string.IsNullOrWhiteSpace(rule) && !selectorsByName.TryGetValue(rule, out selector)
            )
                throw new InvalidOperationException(
                    $"Bff:Frontends:{name}:Selection:Rule '{rule}' is not a registered frontend selection rule."
                );

            frontends.Add(new BffFrontend(name, frontendOptions, selector));
        }

        return frontends;
    }

    private readonly Lazy<IReadOnlyList<BffFrontend>> _frontends = new(() =>
        Create(options.Value, selectors)
    );

    /// <summary>
    /// Gets the frontends that the BFF serves, ordered by name.
    /// </summary>
    public IReadOnlyList<BffFrontend> Frontends => _frontends.Value;

    /// <summary>
    /// Gets a frontend by its name.
    /// </summary>
    /// <param name="name">The name of the frontend.</param>
    /// <param name="frontend">The frontend, when one has the name.</param>
    /// <returns><see langword="true"/> when a frontend has the name; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out BffFrontend? frontend)
    {
        foreach (var candidate in Frontends)
        {
            if (!string.Equals(candidate.Name, name, StringComparison.Ordinal))
                continue;

            frontend = candidate;
            return true;
        }

        frontend = null;
        return false;
    }

    /// <summary>
    /// Selects the frontend that serves a request, and keeps the answer for the rest of the request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The frontend of the request, or <see langword="null"/> when no frontend serves it.</returns>
    /// <exception cref="InvalidOperationException">The selection rules of two frontends select the request.</exception>
    public BffFrontend? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var feature = httpContext.Features.Get<BffFrontendFeature>();
        if (feature is not null)
            return feature.Frontend;

        BffFrontend? selected = null;
        BffFrontend? fallback = null;
        foreach (var frontend in Frontends)
        {
            if (frontend.IsFallback)
            {
                fallback ??= frontend;
                continue;
            }

            if (!frontend.IsMatch(httpContext.Request))
                continue;

            if (selected is not null)
                throw new InvalidOperationException(
                    $"The request is selected by the BFF frontends '{selected.Name}' and '{frontend.Name}'; their selection rules must not overlap."
                );

            selected = frontend;
        }

        var result = selected ?? fallback;
        httpContext.Features.Set(new BffFrontendFeature(result));
        return result;
    }

    /// <summary>
    /// Selects the frontend that serves a request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The frontend of the request.</returns>
    /// <exception cref="InvalidOperationException">
    /// No frontend serves the request, or the selection rules of two frontends select it.
    /// </exception>
    public BffFrontend GetRequired(HttpContext httpContext) =>
        Resolve(httpContext)
        ?? throw new InvalidOperationException(
            "No BFF frontend serves this request: a frontend's Bff:Frontends:<name>:Selection must select it, or one frontend must have no selection rule."
        );
}
