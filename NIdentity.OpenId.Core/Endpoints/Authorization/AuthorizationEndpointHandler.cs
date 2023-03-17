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
using NIdentity.OpenId.Endpoints.Authorization.Mediator;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Middleware;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization;

internal class AuthorizationEndpointHandler : IRequestResponseHandler<AuthorizationEndpointRequest, IOpenIdResult>
{
    private IMediator Mediator { get; }

    public AuthorizationEndpointHandler(IMediator mediator)
    {
        Mediator = mediator;
    }

    /// <inheritdoc />
    public async ValueTask<IOpenIdResult> HandleAsync(AuthorizationEndpointRequest request, CancellationToken cancellationToken)
    {
        var endpointContext = request.EndpointContext;
        var httpContext = endpointContext.HttpContext;

        var authorizationRequestStringValues = await GetAuthorizationRequestStringValues(endpointContext, cancellationToken);
        httpContext.Features.Set<IOpenIdMessageFeature>(new OpenIdMessageFeature { OpenIdMessage = authorizationRequestStringValues });

        var authorizationRequestUnion = await GetAuthorizationRequestUnionAsync(authorizationRequestStringValues, cancellationToken);
        httpContext.Features.Set<IOpenIdMessageFeature>(new OpenIdMessageFeature { OpenIdMessage = authorizationRequestUnion });

        await ValidateAuthorizationRequestAsync(authorizationRequestUnion, cancellationToken);

        var authenticateResult = await AuthenticateAsync(endpointContext, cancellationToken);

        return await GetAuthorizationResponseAsync(
            endpointContext,
            authorizationRequestUnion,
            authenticateResult,
            cancellationToken);
    }

    private async ValueTask<IAuthorizationRequestStringValues> GetAuthorizationRequestStringValues(
        OpenIdEndpointContext endpointContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationRequestStringValuesRequest(endpointContext),
            cancellationToken);

    private async ValueTask<IAuthorizationRequestUnion> GetAuthorizationRequestUnionAsync(
        IAuthorizationRequestStringValues message,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationRequestUnionRequest(message),
            cancellationToken);

    private async ValueTask ValidateAuthorizationRequestAsync(
        IAuthorizationRequestUnion authorizationRequest,
        CancellationToken cancellationToken) =>
        await Mediator.PublishAsync(
            new ValidateAuthorizationRequestRequest(authorizationRequest),
            cancellationToken);

    private async ValueTask<AuthenticateResult> AuthenticateAsync(
        OpenIdEndpointContext endpointContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new AuthenticateRequest(endpointContext),
            cancellationToken);

    private async ValueTask<IOpenIdResult> GetAuthorizationResponseAsync(
        OpenIdEndpointContext endpointContext,
        IAuthorizationRequestUnion authorizationRequest,
        AuthenticateResult authenticateResult,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationResponseRequest(
                endpointContext,
                authorizationRequest,
                authenticateResult),
            cancellationToken);
}
