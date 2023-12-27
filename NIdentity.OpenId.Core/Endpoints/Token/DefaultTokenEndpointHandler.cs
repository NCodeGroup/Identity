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
using NIdentity.OpenId.Clients;
using NIdentity.OpenId.Endpoints.Token.Commands;
using NIdentity.OpenId.Endpoints.Token.Messages;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Endpoints.Token;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
public class DefaultTokenEndpointHandler(
    OpenIdServer openIdServer,
    IOpenIdContextFactory contextFactory,
    IClientAuthenticationService clientAuthenticationService
) : IOpenIdEndpointProvider,
    ICommandHandler<ValidateTokenRequestCommand>
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdErrorFactory ErrorFactory => OpenIdServer.ErrorFactory;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapGet(OpenIdConstants.EndpointPaths.Token, HandleRouteAsync)
        .OpenIdDiscoverable(OpenIdConstants.EndpointNames.Token);

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
            return authResult.Error.AsResult();
        }

        if (!authResult.HasClient)
        {
            return ErrorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsResult();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tokenRequest = TokenRequest.Load(OpenIdServer, formData);

        var openIdClient = authResult.Client;
        var tokenRequestContext = new TokenRequestContext(openIdContext, openIdClient, tokenRequest);

        await mediator.SendAsync(
            new ValidateTokenRequestCommand(tokenRequestContext),
            cancellationToken);

        // TODO: issue token(s)

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenRequestCommand command,
        CancellationToken cancellationToken)
    {
        var tokenRequestContext = command.TokenRequestContext;
        var (openIdContext, openIdClient, tokenRequest) = tokenRequestContext;
        var clientSettings = openIdClient.Settings;

        if (!string.IsNullOrEmpty(tokenRequest.ClientId) &&
            !string.Equals(
                tokenRequest.ClientId,
                openIdClient.ClientId,
                StringComparison.Ordinal))
        {
            throw ErrorFactory
                .InvalidRequest("The 'client_id' parameter, when specified, must be identical to the authenticated client identifier.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // TODO: validate client

        // TODO: validate request

        var grantType = tokenRequest.GrantType;
        if (string.IsNullOrEmpty(grantType))
        {
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.GrantType)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // grant_types_supported
        if (!clientSettings.GrantTypesSupported.Contains(grantType))
        {
            // unauthorized_client
            throw ErrorFactory
                .UnauthorizedClient("The client is not allowed to use the specified grant type.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // TODO: check DPoP

        await ValueTask.CompletedTask;
        throw new NotImplementedException();
    }
}
