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
) : IOpenIdEndpointProvider
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapGet(OpenIdConstants.EndpointPaths.Token, HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Token)
        .OpenIdDiscoverable();

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
            return TypedResults.BadRequest();
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

        OpenIdClient openIdClient;
        if (authResult.IsConfidential)
        {
            openIdClient = authResult.ConfidentialClient;
        }
        else if (authResult.IsPublic)
        {
            openIdClient = authResult.PublicClient;
        }
        else
        {
            return TypedResults.BadRequest();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tokenRequest = TokenRequest.Load(OpenIdServer, formData);

        var tokenRequestContext = new TokenRequestContext(openIdContext, openIdClient, tokenRequest);

        // TODO: validate client

        // TODO: validate request
        if (!string.IsNullOrEmpty(tokenRequest.ClientId) &&
            string.Equals(
                tokenRequest.ClientId,
                openIdClient.ClientId,
                StringComparison.Ordinal))
        {
            return TypedResults.BadRequest();
        }

        // TODO: check DPoP

        // TODO: issue token(s)

        throw new NotImplementedException();
    }
}
