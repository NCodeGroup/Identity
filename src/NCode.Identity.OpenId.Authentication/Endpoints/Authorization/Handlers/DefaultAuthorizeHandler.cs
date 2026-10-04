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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthorizeCommand"/> message.
/// </summary>
internal class DefaultAuthorizeHandler(ILogger<DefaultAuthorizeHandler> logger)
    : ICommandResponseHandler<AuthorizeCommand, AuthorizeDisposition>
{
    private ILogger<DefaultAuthorizeHandler> Logger { get; } = logger;

    internal virtual AuthorizeDisposition Failed(IOpenIdError error) => new(error);

    internal virtual AuthorizeDisposition Authorized() => new(ChallengeRequired: false);

    internal virtual AuthorizeDisposition ChallengeRequired() => new(ChallengeRequired: true);

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

        if (promptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
        {
            Logger.ClientRequestedAccountCreation();
            return ChallengeRequired();
        }

        var reAuthenticate =
            promptTypes.Contains(OpenIdConstants.PromptTypes.Login)
            || promptTypes.Contains(OpenIdConstants.PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            Logger.ClientRequestedReAuthentication();
            return ChallengeRequired();
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
            return InteractionRequired(errorFactory, noPrompt);
        }

        // TODO: check consent

        return Authorized();
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
