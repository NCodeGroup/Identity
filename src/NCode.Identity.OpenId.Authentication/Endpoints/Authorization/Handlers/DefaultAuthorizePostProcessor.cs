// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Mediator;
using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a post-processor for the <see cref="AuthorizeCommand"/> message.
/// </summary>
internal class DefaultAuthorizePostProcessor
    : ICommandResponsePostProcessor<AuthorizeCommand, AuthorizeDisposition>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask PostProcessAsync(
        AuthorizeCommand command,
        AuthorizeDisposition response,
        CancellationToken cancellationToken
    )
    {
        if (
            response.HasError
            && response.Error.State == null
            && command.AuthorizationRequest.State != null
        )
        {
            response.Error.State = command.AuthorizationRequest.State;
        }

        return ValueTask.CompletedTask;
    }
}
