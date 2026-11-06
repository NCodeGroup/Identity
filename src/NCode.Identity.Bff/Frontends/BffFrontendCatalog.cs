// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Provides the frontends that the BFF serves and selects the frontend of a request.
/// </summary>
/// <remarks>
/// <para>
/// The set of frontends is built on first use, so resolving a service that depends on the catalog never reads or
/// validates the options. When the options change, the catalog builds a candidate set and replaces the current set
/// only with a candidate that passes every registered check; an invalid candidate is logged and the current set stays.
/// A request keeps the frontend it resolved.
/// </para>
/// <para>
/// A frontend that names a selection rule is selected by that rule. The frontend that names no rule serves every
/// request that no other frontend selects. A request that two frontends select fails closed.
/// </para>
/// </remarks>
// See ADR-1442.
internal sealed class BffFrontendCatalog : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffFrontendCatalog"/> class.
    /// </summary>
    /// <param name="optionsFactory">Creates and validates the options of the BFF.</param>
    /// <param name="changeTokenSources">Signal that the options changed.</param>
    /// <param name="selectors">The registered frontend selection rules.</param>
    /// <param name="checks">The checks that every set of frontends must pass.</param>
    /// <param name="logger">The logger.</param>
    public BffFrontendCatalog(
        IOptionsFactory<BffOptions> optionsFactory,
        IEnumerable<IOptionsChangeTokenSource<BffOptions>> changeTokenSources,
        IEnumerable<IBffFrontendSelector> selectors,
        IEnumerable<IBffFrontendSetCheck> checks,
        ILogger<BffFrontendCatalog> logger
    )
    {
        OptionsFactory = optionsFactory;
        Selectors = [.. selectors];
        Checks = [.. checks];
        Logger = logger;

        var sources = changeTokenSources
            .Where(x =>
                string.Equals(
                    x.Name ?? Microsoft.Extensions.Options.Options.DefaultName,
                    Microsoft.Extensions.Options.Options.DefaultName,
                    StringComparison.Ordinal
                )
            )
            .ToArray();

        if (sources.Length > 0)
        {
            Subscription = ChangeToken.OnChange(
                () => new CompositeChangeToken([.. sources.Select(x => x.GetChangeToken())]),
                Reload
            );
        }
    }

    private IOptionsFactory<BffOptions> OptionsFactory { get; }

    private IBffFrontendSelector[] Selectors { get; }

    private IBffFrontendSetCheck[] Checks { get; }

    private ILogger<BffFrontendCatalog> Logger { get; }

    private IDisposable? Subscription { get; }

    private Lock Gate { get; } = new();

    private List<IBffFrontendSetCheck> AddedChecks { get; } = [];

    private CancellationTokenSource ChangeSource { get; set; } = new();

    private BffFrontendSet? _current;

    private BffFrontendSet Build()
    {
        var options = OptionsFactory.Create(Microsoft.Extensions.Options.Options.DefaultName);
        var set = BffFrontendSet.Create(options, Selectors);

        var failures = new List<string>();
        foreach (var check in Checks)
            check.Check(set, failures);
        foreach (var check in AddedChecks)
            check.Check(set, failures);

        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        return set;
    }

    private void Reload()
    {
        BffFrontendSet next;
        CancellationTokenSource previous;

        lock (Gate)
        {
            try
            {
                next = Build();
            }
            catch (Exception exception)
                when (exception is OptionsValidationException or InvalidOperationException)
            {
                Logger.FrontendsChangeRejected(exception.Message);
                return;
            }

            Volatile.Write(ref _current, next);
            previous = ChangeSource;
            ChangeSource = new CancellationTokenSource();
        }

        if (Logger.IsEnabled(LogLevel.Information))
            Logger.FrontendsChanged(string.Join(", ", next.Frontends.Select(x => x.Name)));
        previous.Cancel();
    }

    /// <summary>
    /// Gets the current set of frontends and the options that they were built from.
    /// </summary>
    /// <exception cref="InvalidOperationException">The first set of frontends is not valid.</exception>
    public BffFrontendSet Current
    {
        get
        {
            var current = Volatile.Read(ref _current);
            if (current is not null)
                return current;

            lock (Gate)
            {
                current = Volatile.Read(ref _current);
                if (current is null)
                {
                    current = Build();
                    Volatile.Write(ref _current, current);
                }

                return current;
            }
        }
    }

    /// <summary>
    /// Gets the frontends that the BFF serves, ordered by name.
    /// </summary>
    public IReadOnlyList<BffFrontend> Frontends => Current.Frontends;

    /// <summary>
    /// Adds a check that the current set and every later candidate set must pass.
    /// </summary>
    /// <param name="check">The check to add.</param>
    /// <exception cref="InvalidOperationException">The current set fails the check.</exception>
    public void AddCheck(IBffFrontendSetCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        var failures = new List<string>();
        check.Check(Current, failures);
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        lock (Gate)
        {
            AddedChecks.Add(check);
        }
    }

    /// <summary>
    /// Gets a token that signals when the set of frontends is replaced.
    /// </summary>
    /// <returns>A change token that fires once for the replacement of the current set.</returns>
    public IChangeToken GetChangeToken()
    {
        lock (Gate)
        {
            return new CancellationChangeToken(ChangeSource.Token);
        }
    }

    /// <summary>
    /// Gets a frontend by its name.
    /// </summary>
    /// <param name="name">The name of the frontend.</param>
    /// <param name="frontend">The frontend, when one has the name.</param>
    /// <returns><see langword="true"/> when a frontend has the name; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out BffFrontend? frontend) =>
        Current.TryGet(name, out frontend);

    /// <inheritdoc />
    public void Dispose() => Subscription?.Dispose();

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

        var result = Current.Select(httpContext.Request);
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
