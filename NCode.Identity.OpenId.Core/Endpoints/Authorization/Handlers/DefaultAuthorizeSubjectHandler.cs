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
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthorizeSubjectCommand"/> message.
/// </summary>
[PublicAPI]
public class DefaultAuthorizeSubjectHandler(
    ILogger<DefaultAuthorizeSubjectHandler> logger
) : ICommandResponseHandler<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>
{
    private ILogger<DefaultAuthorizeSubjectHandler> Logger { get; } = logger;

    internal virtual AuthorizeSubjectDisposition Failed(IOpenIdError error) => new(error);
    internal virtual AuthorizeSubjectDisposition Authorized() => new(ChallengeRequired: false);
    internal virtual AuthorizeSubjectDisposition ChallengeRequired() => new(ChallengeRequired: true);
    internal virtual AuthorizeSubjectDisposition LoginRequired(IOpenIdErrorFactory errorFactory) => Failed(errorFactory.LoginRequired());
    internal virtual AuthorizeSubjectDisposition InteractionRequired(IOpenIdErrorFactory errorFactory, bool noPrompt) => noPrompt ? LoginRequired(errorFactory) : ChallengeRequired();

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    public async ValueTask<AuthorizeSubjectDisposition> HandleAsync(
        AuthorizeSubjectCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, authenticationTicket) = command;

        var errorFactory = openIdContext.ErrorFactory;

        var promptTypes = authorizationRequest.PromptTypes;

        if (promptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
        {
            Logger.LogInformation("Client requested account creation.");
            return ChallengeRequired();
        }

        var reAuthenticate =
            promptTypes.Contains(OpenIdConstants.PromptTypes.Login) ||
            promptTypes.Contains(OpenIdConstants.PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            Logger.LogInformation("Client requested re-authentication.");
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
        if (subjectDisposition.HasOpenIdError)
        {
            return InteractionRequired(errorFactory, noPrompt);
        }

        // TODO: check consent

        return Authorized();
    }

    private static async ValueTask<OperationDisposition> ValidateSubjectAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;
        var operationDisposition = new OperationDisposition();

        await mediator.SendAsync(
            new ValidateSubjectCommand(
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
