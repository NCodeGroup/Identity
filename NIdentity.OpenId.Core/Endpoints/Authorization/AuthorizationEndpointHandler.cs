#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Endpoints.Authorization.Requests;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Requests.Authorization;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization;

internal class AuthorizationEndpointHandler : IRequestResponseHandler<AuthorizationEndpointRequest, IHttpResult>
{
    private IMediator Mediator { get; }

    public AuthorizationEndpointHandler(IMediator mediator)
    {
        Mediator = mediator;
    }

    /// <inheritdoc />
    public async ValueTask<IHttpResult> HandleAsync(AuthorizationEndpointRequest request, CancellationToken cancellationToken)
    {
        var authorizationRequest = await GetAuthorizationRequestAsync(request.HttpContext, cancellationToken);

        await ValidateAuthorizationRequestAsync(authorizationRequest, cancellationToken);

        var authenticateResult = await AuthenticateAsync(request.HttpContext, cancellationToken);

        return await GetAuthorizationResponseAsync(authorizationRequest, authenticateResult, cancellationToken);
    }

    private async ValueTask<IAuthorizationRequest> GetAuthorizationRequestAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationRequestRequest(httpContext),
            cancellationToken);

    private async ValueTask ValidateAuthorizationRequestAsync(IAuthorizationRequest authorizationRequest, CancellationToken cancellationToken) =>
        await Mediator.PublishAsync(
            new ValidateAuthorizationRequestRequest(authorizationRequest),
            cancellationToken);

    private async ValueTask<AuthenticateResult> AuthenticateAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new AuthenticateRequest(httpContext),
            cancellationToken);

    private async ValueTask<IHttpResult> GetAuthorizationResponseAsync(
        IAuthorizationRequest authorizationRequest,
        AuthenticateResult authenticateResult,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationResponseRequest(authorizationRequest, authenticateResult),
            cancellationToken);
}
