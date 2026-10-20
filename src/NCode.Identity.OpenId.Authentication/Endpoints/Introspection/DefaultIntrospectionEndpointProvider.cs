#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the <c>OAuth 2.0 Token
/// Introspection</c> endpoint (<see href="https://datatracker.ietf.org/doc/html/rfc7662">RFC 7662</see>). The endpoint
/// is a thin HTTP shell: it authenticates the caller, resolves the resource server a caller bound to one acts for, and
/// parses the request, then delegates to the mediator to resolve the token's active state and the claims the caller may
/// see. A caller acting for a resource server may request the response as a signed, and optionally encrypted, JSON Web
/// Token (JWT) (<see href="https://datatracker.ietf.org/doc/html/rfc9701">RFC 9701</see>).
/// </summary>
// See ADR-0029 and ADR-0168.
internal class DefaultIntrospectionEndpointProvider(
    TimeProvider timeProvider,
    IClientAuthenticationService clientAuthenticationService,
    IResourceServerCredentialResolver resourceServerCredentialResolver,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IJwtEncoder jwtEncoder
) : IEndpointProvider
{
    private const string JwtResponseContentType = "application/token-introspection+jwt";
    private const string TokenIntrospectionClaim = "token_introspection";
    private const string ActiveMember = "active";

    private TimeProvider TimeProvider { get; } = timeProvider;

    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;

    private IResourceServerCredentialResolver ResourceServerCredentialResolver { get; } =
        resourceServerCredentialResolver;

    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;

    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.Introspection, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.Introspection)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable()
            .AcceptResourceServerClients();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    // RFC 9701 §4: a resource server requests a JWT response by listing its media type in the Accept header.
    private static bool AcceptsJwtResponse(HttpContext httpContext)
    {
        var accept = httpContext.Request.Headers.Accept;
        if (!MediaTypeHeaderValue.TryParseList(accept, out var mediaTypes))
        {
            return false;
        }

        return mediaTypes.Any(mediaType =>
            mediaType.MediaType.Equals(JwtResponseContentType, StringComparison.OrdinalIgnoreCase)
            && mediaType.Quality is not 0
        );
    }

    private static bool RegistersEncryptedResponses(OpenIdClient openIdClient) =>
        openIdClient.Settings.TryGetValue(
            OpenIdSettingKeys.IntrospectionEncryptedResponseAlg,
            out var algorithm
        ) && !string.IsNullOrEmpty(algorithm);

    /// <summary>
    /// Introspect a token (RFC 7662).
    /// </summary>
    /// <remarks>
    /// Reports the active state and metadata of an access or refresh token (OAuth 2.0 Token Introspection,
    /// RFC 7662). The caller authenticates, then submits the token to inspect as application/x-www-form-urlencoded
    /// form data. An access token is reported active only to a client acting for a resource server the token names in
    /// its audience, and a refresh token only to the client it was issued to. A client acting for a resource server
    /// receives a signed, and optionally encrypted, JWT (application/token-introspection+jwt, RFC 9701) when it lists
    /// that media type in its Accept header.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">Whether the token is active and, when active, the claims the caller may see.</response>
    /// <response code="406">The requested response form is not available to the caller.</response>
    internal virtual async ValueTask<IResult> HandleRouteAsync(
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

        var openIdClient = authResult.Client;
        var acceptsJwtResponse = AcceptsJwtResponse(httpContext);

        await using var resolution = openIdClient.ResourceServerId is { } resourceServerId
            ? await ResourceServerCredentialResolver.ResolveByIdAsync(
                openIdContext,
                resourceServerId,
                cancellationToken
            )
            : null;

        var refusal = Refuse(openIdContext, openIdClient, resolution, acceptsJwtResponse);
        if (refusal is not null)
        {
            return refusal.AsHttpResult();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var introspectionRequest = TokenIntrospectionRequest.Load(openIdEnvironment, formData);

        if (string.IsNullOrEmpty(introspectionRequest.Token))
        {
            return errorFactory
                .InvalidRequest($"The '{OpenIdConstants.Parameters.Token}' parameter is required.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var resourceServer = resolution?.Single;
        var result = new IntrospectionResult { Active = false };

        await mediator.SendAsync(
            new IntrospectTokenCommand(
                openIdContext,
                openIdClient,
                resourceServer,
                introspectionRequest,
                result
            ),
            cancellationToken
        );

        // RFC 7662 §2.2: an inactive token is reported without any other member, whatever a handler contributed.
        if (!result.Active)
        {
            result.Claims.Clear();
        }

        if (acceptsJwtResponse && resourceServer is not null)
        {
            var jwt = await EncodeJwtResponseAsync(
                openIdContext,
                openIdClient,
                resourceServer,
                result,
                cancellationToken
            );

            return TypedResults.Text(jwt, JwtResponseContentType);
        }

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }

    // A caller acting for a resource server must authenticate and act for an enabled resource server (RFC 9701 §3, §4);
    // a JWT response is defined only for such a caller, and a caller that registered encrypted responses never receives
    // one in the clear.
    private static IOpenIdError? Refuse(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ResourceServerCredentialResolution? resolution,
        bool acceptsJwtResponse
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        if (resolution is null)
        {
            return acceptsJwtResponse
                ? errorFactory
                    .InvalidRequest(
                        "A JWT introspection response is available only to a client that acts for a resource server."
                    )
                    .WithStatusCode(StatusCodes.Status406NotAcceptable)
                : null;
        }

        if (!openIdClient.IsConfidential || resolution.Single is null)
        {
            return errorFactory.InvalidClient().WithStatusCode(StatusCodes.Status400BadRequest);
        }

        if (!acceptsJwtResponse && RegistersEncryptedResponses(openIdClient))
        {
            return errorFactory
                .InvalidRequest(
                    $"The client registers encrypted introspection responses; request '{JwtResponseContentType}'."
                )
                .WithStatusCode(StatusCodes.Status406NotAcceptable);
        }

        return null;
    }

    // RFC 9701 §5: the response is a JWT typed 'token-introspection+jwt' that carries 'iss', the resource server as
    // 'aud', 'iat', and the introspection response as 'token_introspection' — and no 'sub' or 'exp', so it cannot pass for
    // an access token. It is signed and encrypted as the client registered (ADR-0155).
    private async ValueTask<string> EncodeJwtResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ResolvedResourceServer resourceServer,
        IntrospectionResult result,
        CancellationToken cancellationToken
    )
    {
        var credentials = await ClientJwtCredentialSelector.SelectCredentialsAsync(
            openIdContext,
            openIdClient,
            ClientJwtProtectionSettings.Introspection,
            cancellationToken
        );

        if (credentials.SigningCredentials is null)
        {
            throw openIdContext
                .ErrorFactory.Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithDescription(
                    $"The client registers no '{OpenIdSettingNames.IntrospectionSignedResponseAlg}', and a JWT introspection response is always signed."
                )
                .WithStatusCode(StatusCodes.Status500InternalServerError)
                .AsException();
        }

        var tokenIntrospection = new Dictionary<string, object>(
            result.Claims,
            StringComparer.Ordinal
        )
        {
            [ActiveMember] = result.Active,
        };

        var parameters = new JwtEncodingParameters
        {
            TokenType = JoseTokenTypes.TokenIntrospectionJwt,
            EnsureTokenLifetime = false,

            SigningCredentials = credentials.SigningCredentials,
            EncryptionCredentials = credentials.EncryptionCredentials,

            Issuer = openIdContext.Tenant.Issuer,
            Audience = resourceServer.Identifier,
            IssuedAt = TimeProvider.GetUtcNowWithPrecisionInSeconds(),

            ExtraPayloadClaims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [TokenIntrospectionClaim] = tokenIntrospection,
            },
        };

        return JwtEncoder.Encode(parameters);
    }
}
