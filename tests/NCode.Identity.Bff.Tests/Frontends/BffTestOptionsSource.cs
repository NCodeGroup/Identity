// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Tests.Frontends;

/// <summary>
/// Supplies the options of the BFF to a catalog and lets a test change them, make them fail and signal the change.
/// </summary>
internal sealed class BffTestOptionsSource(BffOptions initial)
    : IOptionsFactory<BffOptions>,
        IOptionsChangeTokenSource<BffOptions>
{
    private BffOptions Options { get; set; } = initial;

    private Exception? Failure { get; set; }

    private CancellationTokenSource Source { get; set; } = new();

    /// <inheritdoc />
    public string? Name => Microsoft.Extensions.Options.Options.DefaultName;

    /// <inheritdoc />
    public BffOptions Create(string name) => Failure is null ? Options : throw Failure;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => new CancellationChangeToken(Source.Token);

    /// <summary>
    /// Replaces the options and signals the change.
    /// </summary>
    /// <param name="next">The options that <see cref="Create"/> returns from now on.</param>
    public void Change(BffOptions next)
    {
        Failure = null;
        Options = next;
        Fire();
    }

    /// <summary>
    /// Makes <see cref="Create"/> throw and signals the change.
    /// </summary>
    /// <param name="failure">The exception that <see cref="Create"/> throws.</param>
    public void Fail(Exception failure)
    {
        Failure = failure;
        Fire();
    }

    private void Fire()
    {
        var previous = Source;
        Source = new CancellationTokenSource();
        previous.Cancel();
    }
}
