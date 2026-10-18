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
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
/// <remarks>
/// Extension grants (<see href="https://datatracker.ietf.org/doc/html/rfc6749#section-4.5">RFC 6749 §4.5</see>) are
/// supported without modifying this endpoint: register an <see cref="ITokenGrantHandler"/> whose
/// <see cref="ITokenGrantHandler.GrantTypes"/> includes the extension grant's absolute-URI identifier, and the
/// grant-selection pipeline routes matching requests to it.
/// </remarks>
internal class DefaultTokenEndpointProvider(
    IClientAuthenticationService clientAuthenticationService,
    IKnownParameterCollectionProvider knownParameterCollectionProvider,
    IProofOfPossessionService proofOfPossessionService,
    ILogger<DefaultTokenEndpointProvider> logger
) : IEndpointProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } =
        knownParameterCollectionProvider;
    private IProofOfPossessionService ProofOfPossessionService { get; } = proofOfPossessionService;
    private ILogger<DefaultTokenEndpointProvider> Logger { get; } = logger;

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
            .Produces(StatusCodes.Status401Unauthorized)
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
    /// <response code="401">An invalid_client error when the client authenticated via the Authorization header and failed; carries a WWW-Authenticate challenge.</response>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        // RFC 6749 §5.1/§5.2: the token endpoint MUST prevent caching of the sensitive token response. Set it up front
        // so both the success and the error responses carry the no-store headers.
        httpContext.Response.SetNoStore();

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
            // Client-authentication handlers set the status code and any WWW-Authenticate challenge (RFC 6749 §5.2);
            // default to 400 only when the handler left it unspecified.
            var error = authResult.Error;
            error.StatusCode ??= StatusCodes.Status400BadRequest;
            return error.AsHttpResult();
        }

        if (!authResult.HasClient)
        {
            Logger.TokenRequestClientAuthenticationRequired();
            return errorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tokenRequest = TokenRequest.Load(openIdEnvironment, formData);

        var openIdClient = authResult.Client;

        // Evaluate sender-constraining proof of possession (DPoP / mutual-TLS) once for the request; the binding
        // flows to the selected grant handler so token issuance applies the token_type and cnf confirmation uniformly.
        var proofOfPossessionBinding = await ProofOfPossessionService.EvaluateTokenRequestAsync(
            openIdContext,
            openIdClient,
            cancellationToken
        );

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
            proofOfPossessionBinding,
            cancellationToken
        );

        return tokenResponse.AsHttpResult();
    }
}
