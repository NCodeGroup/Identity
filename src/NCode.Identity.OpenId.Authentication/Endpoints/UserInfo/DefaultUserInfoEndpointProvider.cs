// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Auditing;
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
/// When the <c>audit_userinfo_access</c> setting is on, each answer to a request whose access token authenticated is
/// audited as a <c>userinfo.accessed</c> event that names the subject, the client and the claims disclosed, never their
/// values or the token.
/// </summary>
// See ADR-1155 and ADR-1401.
internal class DefaultUserInfoEndpointProvider(
    TimeProvider timeProvider,
    IOpenIdClientResolver clientResolver,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IJwtEncoder jwtEncoder,
    IAuditEventRecorder auditEventRecorder
) : IEndpointProvider
{
    private const string JwtContentType = "application/jwt";

    // A UserInfo access token binds the OpenID identity resource server, which owns the openid/profile/email scopes
    // (ADR-1027); require that audience so a token minted for a different resource server is rejected.
    private static readonly string[] UserInfoAudiences =
    [
        OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
    ];

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;

    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;

    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;

    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

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
            await AuditDeniedAsync(openIdContext, ticket.PrincipalId, null, cancellationToken);
            return InvalidToken(errorFactory);
        }

        var openIdClient = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            clientId,
            cancellationToken
        );
        if (openIdClient is null)
        {
            await AuditDeniedAsync(openIdContext, ticket.PrincipalId, clientId, cancellationToken);
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

        IResult response = credentials.IsProtected
            ? Utf8TextResults.Create(
                EncodeUserInfoJwt(openIdContext, openIdClient, credentials, result),
                JwtContentType
            )
            : TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);

        // Audited once the response is built, so a response that cannot be protected is not recorded as a disclosure.
        // The client's effective settings merge its tenant's with Or, so a client cannot opt out of its tenant's audit
        // trail. See ADR-1401.
        if (openIdClient.Settings.GetValue(OpenIdSettingKeys.AuditUserInfoAccess))
        {
            await AuditEventRecorder.RecordUserInfoAccessedAsync(
                openIdContext,
                openIdClient,
                ticket.PrincipalId,
                result.Claims.Keys,
                cancellationToken
            );
        }

        return response;
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

    // A token that authenticated but names no client the tenant serves discloses nothing; the tenant's settings decide
    // whether the refusal is audited, since no client's settings can be read.
    private async ValueTask AuditDeniedAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string? clientId,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        if (!settings.GetValue(OpenIdSettingKeys.AuditUserInfoAccess))
            return;

        await AuditEventRecorder.RecordUserInfoAccessDeniedAsync(
            openIdContext,
            subjectId,
            clientId,
            OpenIdProtocol.ErrorCodes.InvalidToken,
            cancellationToken
        );
    }

    private static IResult InvalidToken(IOpenIdErrorFactory errorFactory) =>
        errorFactory
            .InvalidToken()
            .WithStatusCode(StatusCodes.Status401Unauthorized)
            .AsHttpResult();
}
