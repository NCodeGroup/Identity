// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Blazor.Client;
using NCode.Identity.Bff.Blazor.Logging;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Provides the authentication state of a Blazor circuit: it revalidates the circuit's connection session every
/// <see cref="IBffConnectionSession.RevalidationInterval"/> and signs the circuit out when the session ended, and it
/// persists the <c>/bff/user</c> document for a WebAssembly client while a BFF page renders.
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
    /// <param name="timeProvider">The provider of the revalidation delay.</param>
    /// <param name="logger">The logger.</param>
    public BffAuthenticationStateProvider(
        PersistentComponentState state,
        IHttpContextAccessor httpContextAccessor,
        IBffUserDocumentFactory documents,
        TimeProvider timeProvider,
        ILogger<BffAuthenticationStateProvider> logger
    )
    {
        State = state;
        HttpContextAccessor = httpContextAccessor;
        Documents = documents;
        TimeProvider = timeProvider;
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
    private TimeProvider TimeProvider { get; }
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

        _ = RevalidateAsync(task, source.Token);
    }

    private void CancelRevalidation(CancellationTokenSource? replacement)
    {
        var previous = Interlocked.Exchange(ref _revalidationSource, replacement);
        if (previous is null)
            return;

        previous.Cancel();
        previous.Dispose();
    }

    private async Task RevalidateAsync(
        Task<AuthenticationState> task,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var state = await task;
            if (state.User.GetBffConnectionSession() is not { } connection)
                return;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(connection.RevalidationInterval, TimeProvider, cancellationToken);

                try
                {
                    var principal = await connection.ValidateAsync(null, cancellationToken);
                    if (principal is null)
                    {
                        SetAuthenticationState(
                            Task.FromResult(
                                new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))
                            )
                        );
                        return;
                    }
                }
                catch (BffStoreUnavailableException exception)
                {
                    Logger.RevalidationFailed(exception);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // the circuit's state changed or the provider was disposed
        }
        catch (Exception exception)
        {
            Logger.RevalidationFailed(exception);
        }
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

        State.PersistAsJson(BffBlazorClientDefaults.UserStateKey, user);
    }
}
