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

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Registration.AspNetCore;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the <c>OpenID Connect</c>
/// <c>UserInfo</c> endpoint
/// (<see href="https://openid.net/specs/openid-connect-core-1_0.html#UserInfo">OpenID Connect Core 5.3</see>). The
/// endpoint is a thin HTTP shell: it authenticates the access token the request presents via the
/// <see cref="AuthenticateResourceRequestCommand"/> mediator command, resolves the client the token was issued to,
/// delegates to the <see cref="GetUserInfoClaimsCommand"/> so that the default and application-provided enrichers
/// contribute the subject's claims, and returns them as JSON — or, when the client registers a signing or an encryption
/// algorithm for its UserInfo responses, as a JWT signed by the tenant and/or encrypted to the client
/// (<see href="https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponse">OpenID Connect Core 5.3.2</see>).
/// </summary>
// See ADR-0155.
internal class DefaultUserInfoEndpointProvider(
    TimeProvider timeProvider,
    IOpenIdClientResolver clientResolver,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IJwtEncoder jwtEncoder
) : IEndpointProvider
{
    private const string JwtContentType = "application/jwt";

    // A UserInfo access token binds the OpenID identity resource server, which owns the openid/profile/email scopes
    // (ADR-0027); require that audience so a token minted for a different resource server is rejected.
    private static readonly string[] UserInfoAudiences =
    [
        OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
    ];

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;

    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;

    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.UserInfo,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.UserInfo)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    /// <summary>
    /// UserInfo endpoint (OpenID Connect).
    /// </summary>
    /// <remarks>
    /// Returns claims about the authenticated end-user identified by the presented access token (OpenID Connect
    /// Core). The access token is sent as a ******; the returned claims depend on the scopes granted
    /// during authorization. The response is JSON, or a JWT (<c>application/jwt</c>) when the client registers a
    /// <c>userinfo_signed_response_alg</c> or a <c>userinfo_encrypted_response_alg</c>.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The authenticated end-user's claims.</response>
    /// <response code="401">The access token is missing, invalid, or expired, or the client it was issued to is not
    /// enabled.</response>
    internal virtual async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var mediator = openIdContext.Mediator;
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var disposition = await mediator.SendAsync<
            AuthenticateResourceRequestCommand,
            AuthenticateSubjectDisposition
        >(
            new AuthenticateResourceRequestCommand(openIdContext, UserInfoAudiences),
            cancellationToken
        );

        if (disposition.HasError)
        {
            var error = disposition.Error;
            error.StatusCode ??= StatusCodes.Status401Unauthorized;
            return error.AsHttpResult();
        }

        if (!disposition.IsAuthenticated)
        {
            return InvalidToken(errorFactory);
        }

        var ticket = disposition.Ticket.Value;

        // The response is protected as the client the access token was issued to registers, so a token whose client is
        // unknown or no longer enabled is not honored.
        var clientId = ticket.Subject.FindFirstValue(JoseClaimNames.Payload.ClientId);
        if (string.IsNullOrEmpty(clientId))
        {
            return InvalidToken(errorFactory);
        }

        var openIdClient = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            clientId,
            cancellationToken
        );
        if (openIdClient is null)
        {
            return InvalidToken(errorFactory);
        }

        var result = new UserInfoResult();

        await mediator.SendAsync(
            new GetUserInfoClaimsCommand(openIdContext, ticket, result.Claims),
            cancellationToken
        );

        var credentials = await ClientJwtCredentialSelector.SelectCredentialsAsync(
            openIdContext,
            openIdClient,
            ClientJwtProtectionSettings.UserInfo,
            cancellationToken
        );

        if (!credentials.IsProtected)
        {
            return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
        }

        var jwt = EncodeUserInfoJwt(openIdContext, openIdClient, credentials, result);

        return TypedResults.Text(jwt, JwtContentType);
    }

    // OpenID Connect Core 1.0 §5.3.2: a signed UserInfo response carries 'iss' and 'aud'; an encrypted one is the same
    // claims encrypted to the client, and one both signed and encrypted is a Nested JWT. Its lifetime is the client's ID
    // token lifetime, so a captured response is not treated as current for longer than an identity assertion.
    private string EncodeUserInfoJwt(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ClientJwtCredentials credentials,
        UserInfoResult result
    )
    {
        var issuedAt = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var lifetime = openIdClient.Settings.GetValue(OpenIdSettingKeys.IdTokenLifetime);

        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = credentials.SigningCredentials,
            EncryptionCredentials = credentials.EncryptionCredentials,

            Issuer = openIdContext.Tenant.Issuer,
            Audience = openIdClient.ClientId,

            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt + lifetime,

            ExtraPayloadClaims = result.Claims,
        };

        return JwtEncoder.Encode(parameters);
    }

    private static IResult InvalidToken(IOpenIdErrorFactory errorFactory) =>
        errorFactory
            .InvalidToken()
            .WithStatusCode(StatusCodes.Status401Unauthorized)
            .AsHttpResult();
}
