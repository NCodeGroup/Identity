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
using NCode.Identity.OpenId.Endpoints.Token.Contexts;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Mediator.Commands;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Endpoints.Token;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
public class DefaultTokenEndpointHandler(
    OpenIdServer openIdServer,
    IOpenIdContextFactory contextFactory,
    IClientAuthenticationService clientAuthenticationService,
    IEnumerable<ITokenGrantHandler> handlers
) : IOpenIdEndpointProvider,
    ICommandHandler<ValidateCommand<TokenRequestContext>>,
    ICommandResponseHandler<SelectCommand<TokenRequestContext, ITokenGrantHandler>, ITokenGrantHandler>
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdErrorFactory ErrorFactory => OpenIdServer.ErrorFactory;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;

    private Dictionary<string, ITokenGrantHandler> Handlers { get; } =
        handlers.ToDictionary(handler => handler.GrantType, StringComparer.Ordinal);

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
            throw ErrorFactory
                .InvalidRequest("Only POST requests with Content-Type 'application/x-www-form-urlencoded' are supported.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
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
            throw error.AsException();
        }

        if (!authResult.HasClient)
        {
            throw ErrorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tokenRequest = TokenRequest.Load(OpenIdServer, formData);

        var tokenRequestContext = new TokenRequestContext(
            openIdContext,
            authResult.Client,
            tokenRequest);

        await mediator.SendAsync(
            ValidateCommand.Create(tokenRequestContext),
            cancellationToken);

        var handler = await mediator.SendAsync(
            SelectCommand.Create(tokenRequestContext).Return<ITokenGrantHandler>(),
            cancellationToken);

        await handler.HandleAsync(
            tokenRequestContext,
            cancellationToken);

        var tokenResponse = await handler.HandleAsync(
            tokenRequestContext,
            cancellationToken);

        return TypedResults.Json(tokenResponse, OpenIdServer.JsonSerializerOptions);
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateCommand<TokenRequestContext> command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, tokenRequest) = command.Context;
        var clientSettings = openIdClient.Settings;

        // client_id
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

        // grant_type
        var grantType = tokenRequest.GrantType;
        if (string.IsNullOrEmpty(grantType))
        {
            // invalid_request
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

        // TODO: validate DPoP

        // redirect_uri
        var redirectUri = tokenRequest.RedirectUri;
        if (redirectUri is null)
        {
            // invalid_request
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

            // fyi, the actual value is validated later by the grant handler
        }

        // scopes
        var scopeCount = tokenRequest.Scopes?.Count ?? 0;
        if (scopeCount == 0)
        {
            // invalid_request
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.Scope)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // TODO: validate resource

        // TODO: validate actual scope values

        // TODO: validate PKCE

        // TODO: validate user/principal

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ITokenGrantHandler> HandleAsync(
        SelectCommand<TokenRequestContext, ITokenGrantHandler> command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var grantType = command.Context.TokenRequest.GrantType ?? string.Empty;
        if (!Handlers.TryGetValue(grantType, out var handler))
        {
            throw ErrorFactory
                .UnsupportedGrantType("The provided grant type is not supported by the authorization server.")
                .AsException();
        }

        return ValueTask.FromResult(handler);
    }
}
