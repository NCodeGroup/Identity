// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="LoadEndSessionRequestCommand"/> handler that parses the
/// <see cref="EndSessionRequest"/> from the current request's query string (GET) or form body (POST).
/// </summary>
internal class DefaultLoadEndSessionRequestHandler
    : ICommandResponseHandler<LoadEndSessionRequestCommand, IEndSessionRequest>
{
    /// <inheritdoc />
    public async ValueTask<IEndSessionRequest> HandleAsync(
        LoadEndSessionRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = command.OpenIdContext;
        var openIdEnvironment = openIdContext.Environment;
        var httpRequest = openIdContext.Http.Request;

        if (HttpMethods.IsPost(httpRequest.Method) && httpRequest.HasFormContentType)
        {
            var form = await httpRequest.TryReadFormAsync(cancellationToken);
            if (form is null)
                throw UnreadableForm.CreateError(openIdContext.ErrorFactory).AsException();

            return EndSessionRequest.Load(openIdEnvironment, form);
        }

        return EndSessionRequest.Load(openIdEnvironment, httpRequest.Query);
    }
}
