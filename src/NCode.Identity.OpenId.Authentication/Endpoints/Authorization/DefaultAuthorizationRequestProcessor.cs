#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationRequestProcessor"/> abstraction.
/// </summary>
internal class DefaultAuthorizationRequestProcessor(IContinueService continueService)
    : IAuthorizationRequestProcessor
{
    private IContinueService ContinueService { get; } = continueService;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> ProcessRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await CoreProcessRequestAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                clientRedirectContext,
                cancellationToken
            );
        }
        catch (HttpResultException exception)
        {
            return ReadOnlyEndpointDisposition.Handled(exception.HttpResult);
        }
        catch (Exception exception)
        {
            var errorFactory = openIdContext.ErrorFactory;
            var (redirectUri, responseMode, state) = clientRedirectContext;
            var openIdError = GetOpenIdError(errorFactory, exception, state);
            var httpResult = new AuthorizationResult(redirectUri, responseMode, openIdError);
            return ReadOnlyEndpointDisposition.Handled(httpResult);
        }
    }

    private static IOpenIdError GetOpenIdError(
        IOpenIdErrorFactory errorFactory,
        Exception exception,
        string? state
    ) =>
        exception switch
        {
            OpenIdException openIdException => openIdException.Error.WithState(state),

            _ => errorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithState(state)
                .WithException(exception),
        };

    private async ValueTask<ReadOnlyEndpointDisposition> CoreProcessRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var redirectUri = clientRedirectContext.RedirectUri;

        // Errors produced by the AuthenticateCommand / AuthorizeCommand already have their state stamped by their
        // post-processors; the two errors created inline below (prompt=none login_required, no-consent-surface
        // interaction_required) bypass that pipeline, so echo the request's state onto them here (OIDC Core 3.1.2.6).
        var state = authorizationRequest.State;

        // the request object may have changed the response mode
        var requestObject = authorizationRequest.OriginalRequestObject;
        var effectiveResponseMode = !string.IsNullOrEmpty(requestObject?.ResponseMode)
            ? requestObject.ResponseMode
            : clientRedirectContext.ResponseMode;

        await mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(
                openIdContext,
                openIdClient,
                authorizationRequest
            ),
            cancellationToken
        );

        var authenticateSubjectDisposition = await mediator.SendAsync<
            AuthenticateCommand,
            AuthenticateSubjectDisposition
        >(
            new AuthenticateCommand(openIdContext, openIdClient, authorizationRequest),
            cancellationToken
        );

        if (authenticateSubjectDisposition.HasError)
        {
            return ReadOnlyEndpointDisposition.Handled(
                new AuthorizationResult(
                    redirectUri,
                    effectiveResponseMode,
                    authenticateSubjectDisposition.Error
                )
            );
        }

        if (!authenticateSubjectDisposition.IsAuthenticated)
        {
            // prompt=none forbids interactive authentication, so report login_required back to the client instead
            // of redirecting the user-agent to the interactive login surface.
            if (authorizationRequest.PromptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                return ReadOnlyEndpointDisposition.Handled(
                    new AuthorizationResult(
                        redirectUri,
                        effectiveResponseMode,
                        openIdContext.ErrorFactory.LoginRequired().WithState(state)
                    )
                );
            }

            return await ChallengeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                cancellationToken
            );
        }

        var authenticationTicket = authenticateSubjectDisposition.Ticket.Value;

        var authorizeSubjectDisposition = await mediator.SendAsync<
            AuthorizeCommand,
            AuthorizeDisposition
        >(
            new AuthorizeCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket
            ),
            cancellationToken
        );

        if (authorizeSubjectDisposition.HasError)
        {
            return ReadOnlyEndpointDisposition.Handled(
                new AuthorizationResult(
                    redirectUri,
                    effectiveResponseMode,
                    authorizeSubjectDisposition.Error
                )
            );
        }

        if (authorizeSubjectDisposition.ChallengeRequired)
        {
            return await ChallengeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                cancellationToken
            );
        }

        if (authorizeSubjectDisposition.ConsentRequired)
        {
            var consentDisposition = await RequestConsentAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                cancellationToken
            );
            if (consentDisposition.WasHandled)
            {
                return consentDisposition;
            }

            // No interactive consent surface is registered to render the consent screen, so the request cannot
            // obtain consent and the client is told interaction is required.
            return ReadOnlyEndpointDisposition.Handled(
                new AuthorizationResult(
                    redirectUri,
                    effectiveResponseMode,
                    openIdContext.ErrorFactory.InteractionRequired().WithState(state)
                )
            );
        }

        var authorizationTicket = await mediator.SendAsync<
            CreateAuthorizationTicketCommand,
            IAuthorizationTicket
        >(
            new CreateAuthorizationTicketCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket
            ),
            cancellationToken
        );

        return ReadOnlyEndpointDisposition.Handled(
            new AuthorizationResult(redirectUri, effectiveResponseMode, authorizationTicket)
        );
    }

    private async ValueTask<ReadOnlyEndpointDisposition> ChallengeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;

        var continueUrl = await GetContinueUrlAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            cancellationToken
        );

        var authenticationProperties = new AuthenticationProperties { RedirectUri = continueUrl };

        authenticationProperties.SetString(
            OpenIdConstants.AuthenticationPropertyItems.TenantId,
            openIdContext.Tenant.TenantId
        );

        // for the following parameters, see the OpenIdConnectHandler class for reference:
        // https://github.com/dotnet/aspnetcore/blob/cd5ca6985645aa4929747bd690109a99a97126e3/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L398

        authenticationProperties.SetParameter(
            OpenIdConstants.Parameters.Prompt,
            // OpenIdConnectHandler requires a string, but we have an IReadOnlyList<string>
            string.Join(OpenIdConstants.ParameterSeparatorChar, authorizationRequest.PromptTypes)
        );

        authenticationProperties.SetParameter(
            OpenIdConstants.Parameters.Scope,
            // OpenIdConnectHandler requires an ICollection<string> but we have an IReadOnlyList<string>
            authorizationRequest.Scopes.ToList()
        );

        authenticationProperties.SetParameter(
            OpenIdConstants.Parameters.MaxAge,
            // OpenIdConnectHandler uses TimeSpan? just like we do
            authorizationRequest.MaxAge
        );

        // additional authentication properties can be set via mediator middleware

        var disposition = await mediator.SendAsync<ChallengeCommand, ReadOnlyEndpointDisposition>(
            new ChallengeCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationProperties
            ),
            cancellationToken
        );
        return disposition;
    }

    private async ValueTask<ReadOnlyEndpointDisposition> RequestConsentAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;

        // The same parked continuation the login challenge uses resumes the authorization request after consent.
        var continueUrl = await GetContinueUrlAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            cancellationToken
        );

        return await mediator.SendAsync<ConsentChallengeCommand, ReadOnlyEndpointDisposition>(
            new ConsentChallengeCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl
            ),
            cancellationToken
        );
    }

    private async ValueTask<string> GetContinueUrlAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    )
    {
        var continueUrl = await ContinueService.GetContinueUrlAsync(
            openIdContext,
            OpenIdConstants.ContinueCodes.Authorization,
            openIdClient.ClientId,
            subjectId: null,
            openIdClient.Settings.GetValue(OpenIdSettingKeys.ContinueAuthorizationLifetime),
            authorizationRequest,
            cancellationToken
        );

        return continueUrl;
    }
}
