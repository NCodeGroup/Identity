#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="EndSessionCommand"/> handler that orchestrates OpenID Connect
/// RP-Initiated Logout: it resolves the subject/session (<see cref="ResolveEndSessionSubjectCommand"/>), clears the OP
/// login, dispatches <see cref="NCode.Identity.OpenId.Authentication.Sessions.OnSessionTerminatedCommand"/> to run the
/// logout side effects (revocation, back-channel
/// notification, and any application extensions), and resolves the validated <c>post_logout_redirect_uri</c>. When
/// front-channel logout or session management are enabled, it also collects the front-channel logout iframe URIs and
/// clears the OP browser-state cookie.
/// </summary>
internal class DefaultEndSessionHandler(
    IOpenIdClientResolver clientResolver,
    IFrontChannelLogoutService frontChannelLogoutService,
    ISessionStateService sessionStateService,
    ILogger<DefaultEndSessionHandler> logger
) : ICommandResponseHandler<EndSessionCommand, EndSessionDisposition>
{
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
    private IFrontChannelLogoutService FrontChannelLogoutService { get; } =
        frontChannelLogoutService;
    private ISessionStateService SessionStateService { get; } = sessionStateService;
    private ILogger<DefaultEndSessionHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<EndSessionDisposition> HandleAsync(
        EndSessionCommand command,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = command.OpenIdContext;
        var request = command.Request;
        var mediator = openIdContext.Mediator;
        var settings = openIdContext.Tenant.SettingsProvider.Collection;

        var subject = await mediator.SendAsync<ResolveEndSessionSubjectCommand, EndSessionSubject>(
            new ResolveEndSessionSubjectCommand(openIdContext, request),
            cancellationToken
        );

        // Clear the OP login cookie.
        await openIdContext.Http.SignOutAsync();

        // Removing the OP browser-state cookie lets session-management relying parties observe the login-state change.
        if (settings.GetValue(OpenIdSettingKeys.SessionManagementSupported))
        {
            SessionStateService.ClearBrowserState(openIdContext);
        }

        // Run the logout side effects (revocation, back-channel notification, and any application extensions).
        await mediator.SendAsync(
            new OnSessionTerminatedCommand(
                openIdContext,
                new SessionTermination(
                    SessionTerminationReasons.Logout,
                    subject.SubjectId,
                    subject.SessionId,
                    subject.ParticipatingClientIds
                )
            ),
            cancellationToken
        );

        var frontChannelLogoutUris = settings.GetValue(
            OpenIdSettingKeys.FrontChannelLogoutSupported
        )
            ? await FrontChannelLogoutService.GetLogoutUrisAsync(
                openIdContext,
                subject,
                cancellationToken
            )
            : [];

        var redirectUri = await ResolveRedirectUriAsync(
            openIdContext,
            subject.ClientId,
            request.PostLogoutRedirectUri,
            request.State,
            cancellationToken
        );

        Logger.EndSessionCompleted(subject.ParticipatingClientIds.Count);

        return new EndSessionDisposition(redirectUri, frontChannelLogoutUris);
    }

    private async ValueTask<string?> ResolveRedirectUriAsync(
        OpenIdContext openIdContext,
        string? clientId,
        string? postLogoutRedirectUri,
        string? state,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(postLogoutRedirectUri) || string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var client = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            clientId,
            cancellationToken
        );
        if (client is null)
        {
            return null;
        }

        if (
            !client.Settings.TryGetValue(
                OpenIdSettingKeys.PostLogoutRedirectUris,
                out var allowedUris
            ) || !allowedUris.Contains(postLogoutRedirectUri, StringComparer.Ordinal)
        )
        {
            Logger.EndSessionPostLogoutRedirectUriRejected(postLogoutRedirectUri, clientId);
            return null;
        }

        if (string.IsNullOrEmpty(state))
        {
            return postLogoutRedirectUri;
        }

        var separator = postLogoutRedirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{postLogoutRedirectUri}{separator}state={Uri.EscapeDataString(state)}";
    }
}
