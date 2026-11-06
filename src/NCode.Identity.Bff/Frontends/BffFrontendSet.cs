// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// One immutable set of the BFF's settings: the validated <see cref="BffOptions"/> and the frontends built from them.
/// </summary>
/// <remarks>
/// <para>
/// A frontend that names a selection rule is selected by that rule. The frontend that names no rule serves every
/// request that no other frontend selects. A request that two frontends select fails closed.
/// </para>
/// </remarks>
// See ADR-1442.
internal sealed class BffFrontendSet
{
    private BffFrontendSet(BffOptions options, IReadOnlyList<BffFrontend> frontends)
    {
        Options = options;
        Frontends = frontends;
    }

    /// <summary>
    /// Creates a set of frontends from validated options.
    /// </summary>
    /// <param name="options">The validated options of the BFF.</param>
    /// <param name="selectors">The registered frontend selection rules.</param>
    /// <returns>The set that holds the options and the frontends built from them.</returns>
    /// <exception cref="InvalidOperationException">
    /// A selection rule is registered more than once, or a frontend names a rule that is not registered.
    /// </exception>
    public static BffFrontendSet Create(
        BffOptions options,
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
            var (name, frontendOptions) in options.Frontends.OrderBy(
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

        return new BffFrontendSet(options, [.. frontends]);
    }

    /// <summary>
    /// Gets the validated options that the frontends were built from.
    /// </summary>
    public BffOptions Options { get; }

    /// <summary>
    /// Gets the frontends that the BFF serves, ordered by name.
    /// </summary>
    public IReadOnlyList<BffFrontend> Frontends { get; }

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
    /// Selects the frontend that serves a request.
    /// </summary>
    /// <param name="request">The request to select a frontend for.</param>
    /// <returns>The frontend of the request, or <see langword="null"/> when no frontend serves it.</returns>
    /// <exception cref="InvalidOperationException">The selection rules of two frontends select the request.</exception>
    public BffFrontend? Select(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        BffFrontend? selected = null;
        BffFrontend? fallback = null;
        foreach (var frontend in Frontends)
        {
            if (frontend.IsFallback)
            {
                fallback ??= frontend;
                continue;
            }

            if (!frontend.IsMatch(request))
                continue;

            if (selected is not null)
                throw new InvalidOperationException(
                    $"The request is selected by the BFF frontends '{selected.Name}' and '{frontend.Name}'; their selection rules must not overlap."
                );

            selected = frontend;
        }

        return selected ?? fallback;
    }
}
