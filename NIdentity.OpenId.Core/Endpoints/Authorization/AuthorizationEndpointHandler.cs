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

using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Handlers;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Requests.Authorization;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization;

internal class AuthorizationEndpointHandler : IOpenIdEndpointHandler
{
    private IHttpResultFactory HttpResultFactory { get; }
    private IRequestResponseHandler<GetAuthorizationRequest, IAuthorizationRequest> GetAuthorizationRequestHandler { get; }
    private IEnumerable<IRequestHandler<ValidateAuthorizationRequest>> ValidateAuthorizationRequestHandlers { get; }

    public AuthorizationEndpointHandler(
        IHttpResultFactory httpResultFactory,
        IRequestResponseHandler<GetAuthorizationRequest, IAuthorizationRequest> getAuthorizationRequestHandler,
        IEnumerable<IRequestHandler<ValidateAuthorizationRequest>> validateAuthorizationRequestHandlers)
    {
        HttpResultFactory = httpResultFactory;
        GetAuthorizationRequestHandler = getAuthorizationRequestHandler;
        ValidateAuthorizationRequestHandlers = validateAuthorizationRequestHandlers;
    }

    /// <inheritdoc />
    public async ValueTask<IHttpResult> HandleAsync(OpenIdEndpointRequest request, CancellationToken cancellationToken)
    {
        var authorizationRequest = await GetAuthorizationRequestAsync(request.HttpContext, cancellationToken);
        try
        {
            await ValidateAuthorizationRequestAsync(authorizationRequest, cancellationToken);

            return await GetAuthorizationResponseAsync(authorizationRequest, cancellationToken);
        }
        catch (Exception exception)
        {
            throw authorizationRequest.AnnotateExceptionWithState(exception);
        }
    }

    private async ValueTask<IAuthorizationRequest> GetAuthorizationRequestAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var request = new GetAuthorizationRequest(httpContext);

        return await GetAuthorizationRequestHandler.HandleAsync(request, cancellationToken);
    }

    private async ValueTask ValidateAuthorizationRequestAsync(IAuthorizationRequest authorizationRequest, CancellationToken cancellationToken)
    {
        var request = new ValidateAuthorizationRequest(authorizationRequest);

        foreach (var handler in ValidateAuthorizationRequestHandlers)
        {
            await handler.HandleAsync(request, cancellationToken);
        }
    }

    private async ValueTask<IHttpResult> GetAuthorizationResponseAsync(IAuthorizationRequest authorizationRequest, CancellationToken cancellationToken)
    {
        // TODO

        return HttpResultFactory.Ok();
    }
}
