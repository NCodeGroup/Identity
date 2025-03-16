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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides the logic for processing authorization requests or continuations for the OpenID Connect authorization endpoint.
/// </summary>
public interface IAuthorizationEndpointLogic
{
    /// <summary>
    /// Processes an <c>OAuth</c> or <c>OpenID Connect</c> authorization request or continuation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="authorizationRequest">The <see cref="IAuthorizationRequest"/> that represents the authorization request.</param>
    /// <param name="clientRedirectContext">The <see cref="ClientRedirectContext"/> that contains information about how to redirect the user-agent.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="EndpointDisposition"/> with the result of processing the request.</returns>
    ValueTask<EndpointDisposition> ProcessRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationEndpointLogic"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultAuthorizationEndpointLogic(
    ILogger<DefaultAuthorizationEndpointLogic> logger,
    IContinueService continueService
) : IAuthorizationEndpointLogic
{
    private ILogger<DefaultAuthorizationEndpointLogic> Logger { get; } = logger;
    private IContinueService ContinueService { get; } = continueService;

    /// <inheritdoc />
    public async ValueTask<EndpointDisposition> ProcessRequestAsync(
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
            return EndpointDisposition.Handled(exception.HttpResult);
        }
        catch (Exception exception)
        {
            var errorFactory = openIdContext.ErrorFactory;
            var (redirectUri, responseMode, state) = clientRedirectContext;
            var openIdError = GetOpenIdError(errorFactory, exception, state);
            var httpResult = new AuthorizationResult(redirectUri, responseMode, openIdError);
            return EndpointDisposition.Handled(httpResult);
        }
    }

    private static IOpenIdError GetOpenIdError(IOpenIdErrorFactory errorFactory, Exception exception, string? state) =>
        exception switch
        {
            OpenIdException openIdException => openIdException.Error
                .WithState(state),

            _ => errorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithState(state)
                .WithException(exception)
        };

    private async ValueTask<EndpointDisposition> CoreProcessRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var redirectUri = clientRedirectContext.RedirectUri;

        // the request object may have changed the response mode
        var requestObject = authorizationRequest.OriginalRequestObject;
        var effectiveResponseMode = !string.IsNullOrEmpty(requestObject?.ResponseMode) ?
            requestObject.ResponseMode :
            clientRedirectContext.ResponseMode;

        await mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(
                openIdContext,
                openIdClient,
                authorizationRequest
            ),
            cancellationToken
        );

        var authenticateSubjectDisposition = await mediator.SendAsync<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>(
            new AuthenticateSubjectCommand(
                openIdContext,
                openIdClient,
                authorizationRequest
            ),
            cancellationToken
        );

        if (authenticateSubjectDisposition.HasError)
        {
            return EndpointDisposition.Handled(
                new AuthorizationResult(
                    redirectUri,
                    effectiveResponseMode,
                    authenticateSubjectDisposition.Error
                )
            );
        }

        if (!authenticateSubjectDisposition.IsAuthenticated)
        {
            return await ChallengeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                cancellationToken
            );
        }

        var authenticationTicket = authenticateSubjectDisposition.Ticket.Value;

        var authorizeSubjectDisposition = await mediator.SendAsync<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>(
            new AuthorizeSubjectCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket
            ),
            cancellationToken
        );

        if (authorizeSubjectDisposition.HasError)
        {
            return EndpointDisposition.Handled(
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

        var authorizationTicket = await mediator.SendAsync<CreateAuthorizationTicketCommand, IAuthorizationTicket>(
            new CreateAuthorizationTicketCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket
            ),
            cancellationToken
        );

        return EndpointDisposition.Handled(
            new AuthorizationResult(
                redirectUri,
                effectiveResponseMode,
                authorizationTicket
            )
        );
    }

    private async ValueTask<EndpointDisposition> ChallengeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var clientSettings = openIdClient.Settings;

        var continueUrl = await ContinueService.GetContinueUrlAsync(
            openIdContext,
            OpenIdConstants.ContinueCodes.Authorization,
            openIdClient.ClientId,
            subjectId: null,
            clientSettings.GetValue(SettingKeys.ContinueAuthorizationLifetime),
            authorizationRequest,
            cancellationToken
        );

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = continueUrl
        };

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

        var disposition = await mediator.SendAsync<ChallengeSubjectCommand, EndpointDisposition>(
            new ChallengeSubjectCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationProperties
            ),
            cancellationToken
        );
        return disposition;
    }
}
