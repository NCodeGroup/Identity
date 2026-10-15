#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthorizeCommand"/> message.
/// </summary>
internal class DefaultAuthorizeHandler(
    IConsentService consentService,
    IAuditEventRecorder auditEventRecorder,
    ILogger<DefaultAuthorizeHandler> logger
) : ICommandResponseHandler<AuthorizeCommand, AuthorizeDisposition>
{
    private IConsentService ConsentService { get; } = consentService;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;
    private ILogger<DefaultAuthorizeHandler> Logger { get; } = logger;

    internal virtual AuthorizeDisposition Failed(IOpenIdError error) => new(error);

    internal virtual AuthorizeDisposition Authorized() => new(ChallengeRequired: false);

    internal virtual AuthorizeDisposition ChallengeRequired() => new(ChallengeRequired: true);

    internal virtual AuthorizeDisposition ConsentRequired() =>
        new(ChallengeRequired: false, ConsentRequired: true);

    internal virtual AuthorizeDisposition LoginRequired(IOpenIdErrorFactory errorFactory) =>
        Failed(errorFactory.LoginRequired());

    internal virtual AuthorizeDisposition InteractionRequired(
        IOpenIdErrorFactory errorFactory,
        bool noPrompt
    ) => noPrompt ? LoginRequired(errorFactory) : ChallengeRequired();

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    public async ValueTask<AuthorizeDisposition> HandleAsync(
        AuthorizeCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, authorizationRequest, authenticationTicket) = command;

        var errorFactory = openIdContext.ErrorFactory;

        var promptTypes = authorizationRequest.PromptTypes;

        // prompt=create / login / select_account each force a one-time interactive step. A continuation is the
        // resumption *after* that step completed (the upstream AuthenticateCommand has already established the fresh
        // authentication for the resumed request), so re-challenging it would loop — the same !IsContinuation guard
        // prompt=consent uses below (ADR-0081).
        if (!authorizationRequest.IsContinuation)
        {
            if (promptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
            {
                Logger.ClientRequestedAccountCreation();
                return ChallengeRequired();
            }

            if (
                promptTypes.Contains(OpenIdConstants.PromptTypes.Login)
                || promptTypes.Contains(OpenIdConstants.PromptTypes.SelectAccount)
            )
            {
                Logger.ClientRequestedReAuthentication();
                return ChallengeRequired();
            }
        }

        var noPrompt = promptTypes.Contains(OpenIdConstants.PromptTypes.None);

        var subjectDisposition = await ValidateSubjectAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authenticationTicket,
            cancellationToken
        );
        if (subjectDisposition.HasError)
        {
            // A no-prompt request that cannot interact is a genuine denial; otherwise the user agent is
            // challenged to interact, which is not an authorization decision.
            if (noPrompt)
            {
                await AuditEventRecorder.RecordAuthorizationDeniedAsync(
                    openIdContext,
                    openIdClient,
                    authenticationTicket.PrincipalId,
                    subjectDisposition.Error.Code,
                    cancellationToken
                );
            }

            return InteractionRequired(errorFactory, noPrompt);
        }

        var consentDisposition = await ValidateConsentAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authenticationTicket,
            noPrompt,
            cancellationToken
        );
        if (consentDisposition.HasValue)
        {
            return consentDisposition.Value;
        }

        await AuditEventRecorder.RecordAuthorizationGrantedAsync(
            openIdContext,
            openIdClient,
            authenticationTicket.PrincipalId,
            cancellationToken
        );
        return Authorized();
    }

    // Enforces durable, granular consent when the client requires it (the `require_consent` setting) or the request
    // forces a fresh decision (`prompt=consent`): a subject whose recorded consent has not yet decided every requested
    // scope is sent to the interactive consent surface; when consent is satisfied the request's issued scopes are
    // narrowed to the granted subset (OIDC down-scoping), unless `prompt=none` forbids interaction (then
    // `consent_required`).
    private async ValueTask<AuthorizeDisposition?> ValidateConsentAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        SubjectAuthentication authenticationTicket,
        bool noPrompt,
        CancellationToken cancellationToken
    )
    {
        var promptConsent = authorizationRequest.PromptTypes.Contains(
            OpenIdConstants.PromptTypes.Consent
        );
        var requireConsent = openIdClient.Settings.GetValue(OpenIdSettingKeys.RequireConsent);

        if (!promptConsent && !requireConsent)
        {
            return null;
        }

        var resolution = await ConsentService.ResolveConsentAsync(
            openIdContext,
            openIdClient.ClientId,
            authenticationTicket.PrincipalId,
            authorizationRequest.Scopes,
            cancellationToken
        );

        // `prompt=consent` forces a fresh decision, but only on the initial request — not when resuming the parked
        // continuation after the subject has just completed the consent screen (which recorded their decision).
        var mustPrompt =
            (promptConsent && !authorizationRequest.IsContinuation) || !resolution.IsDecided;

        if (mustPrompt)
        {
            Logger.ConsentRequired();

            if (noPrompt)
            {
                await AuditEventRecorder.RecordAuthorizationDeniedAsync(
                    openIdContext,
                    openIdClient,
                    authenticationTicket.PrincipalId,
                    OpenIdConstants.ErrorCodes.ConsentRequired,
                    cancellationToken
                );
                return Failed(openIdContext.ErrorFactory.ConsentRequired());
            }

            return ConsentRequired();
        }

        // Consent is satisfied: narrow the issued scopes to the subset the subject granted.
        authorizationRequest.GrantedScopes = resolution.GrantedScopes;
        return null;
    }

    private static async ValueTask<OperationDisposition<IOpenIdError>> ValidateSubjectAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var operationDisposition = new OperationDisposition<IOpenIdError>();

        await mediator.SendAsync(
            new ValidateSubjectAuthenticationCommand(
                openIdContext,
                openIdClient,
                openIdRequest,
                subjectAuthentication,
                operationDisposition
            ),
            cancellationToken
        );

        return operationDisposition;
    }
}
