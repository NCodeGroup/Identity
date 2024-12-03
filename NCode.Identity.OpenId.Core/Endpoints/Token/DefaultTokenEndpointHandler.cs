#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Token;

// TODO: Device Code Grant
// TODO: Ciba Grant
// TODO: Extension Grant

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
public class DefaultTokenEndpointHandler(
    IOpenIdErrorFactory errorFactory,
    IOpenIdContextFactory contextFactory,
    IClientAuthenticationService clientAuthenticationService
) : IOpenIdEndpointProvider
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapGet(OpenIdConstants.EndpointPaths.Token, HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Token)
        .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header) &&
        header.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var isPostVerb = httpContext.Request.Method == HttpMethods.Post;
        if (!isPostVerb || !IsApplicationFormContentType(httpContext))
        {
            return ErrorFactory
                .InvalidRequest("Only POST requests with Content-Type 'application/x-www-form-urlencoded' are supported.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsResult();
        }

        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken);

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken);

        if (authResult.IsError)
        {
            // TODO: add support for 401 with WWW-Authenticate header
            var error = authResult.Error;
            error.StatusCode ??= StatusCodes.Status400BadRequest;
            return error.AsResult();
        }

        if (!authResult.HasClient)
        {
            return ErrorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsResult();
        }

        var openIdEnvironment = openIdContext.Environment;
        var openIdClient = authResult.Client;

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tokenRequest = TokenRequest.Load(openIdEnvironment, formData);

        // for simple validations before selecting the handler and materializing any grants
        await mediator.SendAsync(
            new ValidateTokenRequestCommand(
                openIdContext,
                openIdClient,
                tokenRequest),
            cancellationToken);

        var handler = await mediator.SendAsync<SelectTokenGrantHandlerCommand, ITokenGrantHandler>(
            new SelectTokenGrantHandlerCommand(
                openIdContext,
                openIdClient,
                tokenRequest),
            cancellationToken);

        // the handler is also responsible for validating the request especially grants
        var tokenResponse = await handler.HandleAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            cancellationToken);

        return TypedResults.Json(tokenResponse, openIdEnvironment.JsonSerializerOptions);
    }
}
