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
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token;

// TODO: Device Code Grant
// TODO: Ciba Grant
// TODO: Extension Grant

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
internal class DefaultTokenEndpointProvider(
    IClientAuthenticationService clientAuthenticationService,
    IKnownParameterCollectionProvider knownParameterCollectionProvider
) : IEndpointProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } =
        knownParameterCollectionProvider;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.Token, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.Token)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .WithOpenIdFormParameters(
                KnownParameterCollectionProvider,
                OpenIdConstants.EndpointNames.Token
            )
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Token endpoint (OAuth 2.0).
    /// </summary>
    /// <remarks>
    /// Issues access tokens, refresh tokens, and ID tokens for the supported OAuth 2.0 / OpenID Connect grant types
    /// (for example authorization_code, client_credentials, and refresh_token). The client authenticates and submits
    /// the grant parameters as application/x-www-form-urlencoded form data.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The issued tokens: access_token, token_type, and expires_in, plus id_token, refresh_token, and scope when applicable.</response>
    /// <response code="400">An OAuth 2.0 error response carrying an error code and an optional error_description.</response>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var mediator = openIdContext.Mediator;
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var isPostVerb = httpContext.Request.Method == HttpMethods.Post;
        if (!isPostVerb || !IsApplicationFormContentType(httpContext))
        {
            return errorFactory
                .InvalidRequest(
                    $"Only POST requests with Content-Type '{OpenIdConstants.ContentType}' are supported."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken
        );

        if (authResult.IsError)
        {
            // TODO: add support for 401 with WWW-Authenticate header
            var error = authResult.Error;
            error.StatusCode ??= StatusCodes.Status400BadRequest;
            return error.AsHttpResult();
        }

        if (!authResult.HasClient)
        {
            return errorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tokenRequest = TokenRequest.Load(openIdEnvironment, formData);

        var openIdClient = authResult.Client;

        // for simple validations before selecting the handler and materializing any grants
        await mediator.SendAsync(
            new ValidateTokenRequestCommand(openIdContext, openIdClient, tokenRequest),
            cancellationToken
        );

        var handler = await mediator.SendAsync<SelectTokenGrantHandlerCommand, ITokenGrantHandler>(
            new SelectTokenGrantHandlerCommand(openIdContext, openIdClient, tokenRequest),
            cancellationToken
        );

        // the handler is also responsible for validating the request especially grants
        var tokenResponse = await handler.HandleAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            cancellationToken
        );

        return tokenResponse.AsHttpResult();
    }
}
