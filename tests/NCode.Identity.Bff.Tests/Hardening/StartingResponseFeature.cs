// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http.Features;

namespace NCode.Identity.Bff.Tests.Hardening;

/// <summary>
/// A response feature that records the <c>OnStarting</c> callbacks and runs them when the test starts the response, as
/// a server does before it sends the headers.
/// </summary>
internal sealed class StartingResponseFeature : HttpResponseFeature
{
    private List<(Func<object, Task> Callback, object State)> Callbacks { get; } = [];

    public override void OnStarting(Func<object, Task> callback, object state) =>
        Callbacks.Add((callback, state));

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        // The server runs the callbacks in reverse order of registration.
        for (var index = Callbacks.Count - 1; index >= 0; index--)
        {
            var (callback, state) = Callbacks[index];
            await callback(state);
        }
    }
}
