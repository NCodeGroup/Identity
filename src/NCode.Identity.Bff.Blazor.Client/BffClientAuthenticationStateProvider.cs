// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Provides the authentication state of a WebAssembly app from the BFF session: the user document persisted by the
/// server when the page was prerendered, or a probe of the BFF's user endpoint, kept current by
/// <see cref="BffClientSession"/>.
/// </summary>
internal sealed class BffClientAuthenticationStateProvider
    : AuthenticationStateProvider,
        IDisposable
{
    private BffClientSession Session { get; }

    public BffClientAuthenticationStateProvider(
        BffClientSession session,
        PersistentComponentState state
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        Session = session;

        if (
            state.TryTakeFromJson<JsonElement>(
                BffUserDocumentNames.PersistedStateKey,
                out var document
            ) && BffClientUserReader.TryRead(document, out var user)
        )
        {
            Session.Seed(user);
        }

        Session.Changed += NotifyAuthenticationStateChanged;
    }

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Session.GetAuthenticationStateAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        Session.Changed -= NotifyAuthenticationStateChanged;
    }
}
