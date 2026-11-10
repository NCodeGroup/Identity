// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Blazor.Logging;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Provides the authentication state of a Blazor circuit: it waits through the <see cref="IBffConnectionRevalidator"/>
/// until the circuit's connection session ended, signs the circuit out then or when waiting fails, and it persists the
/// <c>/bff/user</c> document for a WebAssembly client while a BFF page renders.
/// </summary>
internal sealed class BffAuthenticationStateProvider
    : ServerAuthenticationStateProvider,
        IDisposable
{
    private CancellationTokenSource? _revalidationSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffAuthenticationStateProvider"/> class.
    /// </summary>
    /// <param name="state">The state that is persisted for the WebAssembly client.</param>
    /// <param name="httpContextAccessor">The accessor of the request that renders a page.</param>
    /// <param name="documents">The factory of the <c>/bff/user</c> document.</param>
    /// <param name="revalidator">The revalidator that waits until a connection's session ended.</param>
    /// <param name="logger">The logger.</param>
    public BffAuthenticationStateProvider(
        PersistentComponentState state,
        IHttpContextAccessor httpContextAccessor,
        IBffUserDocumentFactory documents,
        IBffConnectionRevalidator revalidator,
        ILogger<BffAuthenticationStateProvider> logger
    )
    {
        State = state;
        HttpContextAccessor = httpContextAccessor;
        Documents = documents;
        Revalidator = revalidator;
        Logger = logger;

        AuthenticationStateChanged += OnAuthenticationStateChanged;
        PersistingSubscription = state.RegisterOnPersisting(
            PersistUserAsync,
            RenderMode.InteractiveWebAssembly
        );
    }

    private PersistentComponentState State { get; }
    private IHttpContextAccessor HttpContextAccessor { get; }
    private IBffUserDocumentFactory Documents { get; }
    private IBffConnectionRevalidator Revalidator { get; }
    private ILogger<BffAuthenticationStateProvider> Logger { get; }
    private PersistingComponentStateSubscription PersistingSubscription { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
        PersistingSubscription.Dispose();
        CancelRevalidation(replacement: null);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        var source = new CancellationTokenSource();
        CancelRevalidation(source);

        _ = RevalidateAsync(task, source.Token).AsTask();
    }

    private static ValueTask<AuthenticationState> SignedOutAsync(
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private void CancelRevalidation(CancellationTokenSource? replacement)
    {
        var previous = Interlocked.Exchange(ref _revalidationSource, replacement);
        if (previous is null)
            return;

        previous.Cancel();
        previous.Dispose();
    }

    private async ValueTask RevalidateAsync(
        Task<AuthenticationState> task,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var state = await task;
            if (state.User.GetBffConnectionSession() is not { } connection)
                return;

            await Revalidator.WaitUntilEndedAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // the circuit's state changed or the provider was disposed
            return;
        }
        catch (Exception exception)
        {
            Logger.RevalidationFaulted(exception);
        }

        SetAuthenticationState(SignedOutAsync(cancellationToken).AsTask());
    }

    private async Task PersistUserAsync()
    {
        if (HttpContextAccessor.HttpContext is not { } httpContext)
            return;

        var metadata = httpContext.GetEndpoint()?.Metadata;
        if (
            metadata?.GetMetadata<BffPageAttribute>() is null
            || metadata.GetMetadata<BffHubAttribute>() is not null
        )
            return;

        var document = await Documents.CreateAsync(httpContext, httpContext.RequestAborted);
        if (document is not { } user)
            return;

        State.PersistAsJson(BffUserDocumentNames.PersistedStateKey, user);
    }
}
