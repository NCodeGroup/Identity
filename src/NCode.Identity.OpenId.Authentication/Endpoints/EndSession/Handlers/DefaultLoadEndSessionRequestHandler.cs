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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
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
            var form = await httpRequest.ReadFormAsync(cancellationToken);
            return EndSessionRequest.Load(openIdEnvironment, form);
        }

        return EndSessionRequest.Load(openIdEnvironment, httpRequest.Query);
    }
}
