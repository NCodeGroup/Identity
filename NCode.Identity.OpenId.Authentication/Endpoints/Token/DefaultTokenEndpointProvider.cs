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
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token;

// TODO: Device Code Grant
// TODO: Ciba Grant
// TODO: Extension Grant

/// <summary>
/// Provides a default implementation of the required services and handlers used by the token endpoint.
/// </summary>
public class DefaultTokenEndpointProvider(
    IOpenIdContextFactory contextFactory,
    IClientAuthenticationService clientAuthenticationService,
    IKnownParameterCollectionProvider knownParameterCollectionProvider
) : IEndpointProvider
{
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } = knownParameterCollectionProvider;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapPost(OpenIdConstants.EndpointPaths.Token, HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Token)
        .WithMetadata(CreateOpenApiOperationMetadata())
        .DisableAntiforgery()
        .WithOpenIdDiscoverable();

    private OpenApiOperation CreateOpenApiOperationMetadata()
    {
        var properties = KnownParameterCollectionProvider.Collection
            .OrderBy(parameter => parameter.Name)
            .ToDictionary(
                parameter => parameter.Name,
                _ => new OpenApiSchema
                {
                    Type = "string",
                    Nullable = true,
                    Default = new OpenApiString(string.Empty),
                });

        return new OpenApiOperation
        {
            OperationId = OpenIdConstants.EndpointNames.Token,
            Tags = [new OpenApiTag { Name = "oidc" }], // TODO: use constant
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [OpenIdConstants.ContentType] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = properties,
                        }
                    }
                }
            }
        };
    }

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header) &&
        header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken
        );

        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var isPostVerb = httpContext.Request.Method == HttpMethods.Post;
        if (!isPostVerb || !IsApplicationFormContentType(httpContext))
        {
            return errorFactory
                .InvalidRequest($"Only POST requests with Content-Type '{OpenIdConstants.ContentType}' are supported.")
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
            new ValidateTokenRequestCommand(
                openIdContext,
                openIdClient,
                tokenRequest
            ),
            cancellationToken
        );

        var handler = await mediator.SendAsync<SelectTokenGrantHandlerCommand, ITokenGrantHandler>(
            new SelectTokenGrantHandlerCommand(
                openIdContext,
                openIdClient,
                tokenRequest
            ),
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
