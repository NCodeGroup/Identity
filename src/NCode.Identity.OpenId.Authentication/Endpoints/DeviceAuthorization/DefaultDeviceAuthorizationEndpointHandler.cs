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
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the device authorization endpoint
/// (RFC 8628 §3.1–3.2).
/// </summary>
internal class DefaultDeviceAuthorizationEndpointHandler(
    TimeProvider timeProvider,
    LinkGenerator linkGenerator,
    ICryptoService cryptoService,
    IClientAuthenticationService clientAuthenticationService,
    IPersistedGrantService persistedGrantService,
    IUserCodeGenerator userCodeGenerator
) : IEndpointProvider
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IUserCodeGenerator UserCodeGenerator { get; } = userCodeGenerator;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.DeviceAuthorization, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.DeviceAuthorization)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<DeviceAuthorizationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Device authorization endpoint (OAuth 2.0 Device Authorization Grant, RFC 8628).
    /// </summary>
    /// <remarks>
    /// Issues a <c>device_code</c> and a human-typable <c>user_code</c> for an input-constrained client. The client
    /// authenticates and submits its requested scopes as application/x-www-form-urlencoded form data, then polls the
    /// token endpoint with the <c>device_code</c> while the end user approves the request at the verification endpoint.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The device_code, user_code, verification_uri, expires_in, and interval.</response>
    /// <response code="400">An OAuth 2.0 error response carrying an error code and an optional error_description.</response>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;
        var tenantId = openIdContext.Tenant.TenantId;

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
        var settings = openIdClient.Settings;

        if (
            settings.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out var grantTypesSupported)
            && !grantTypesSupported.Contains(OpenIdConstants.GrantTypes.DeviceCode)
        )
        {
            return errorFactory
                .UnauthorizedClient("The device authorization grant is not allowed by the client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var scopes = ParseScopes(formData);

        var deviceCode = CryptoService.GenerateUrlSafeKey();

        var userCodeLength = settings.GetValue(OpenIdSettingKeys.UserCodeLength);
        var userCodeDisplay = UserCodeGenerator.Generate(userCodeLength);
        var userCodeNormalized = UserCodeGenerator.Normalize(userCodeDisplay);

        var lifetime = settings.GetValue(OpenIdSettingKeys.DeviceCodeLifetime);
        var interval = settings.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval);
        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var deviceCodeGrant = new PersistedGrant<DeviceCodeGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = tenantId,
            ClientId = openIdClient.ClientId,
            SubjectId = null,
            Payload = new DeviceCodeGrant
            {
                ClientId = openIdClient.ClientId,
                Scopes = scopes,
                Status = DeviceAuthorizationStatus.Pending,
                SubjectAuthentication = null,
                LastPolledWhen = null,
            },
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            PersistedGrantService.CreateGrantId(
                tenantId,
                OpenIdConstants.PersistedGrantTypes.DeviceCode,
                deviceCode
            ),
            deviceCodeGrant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        var userCodeGrant = new PersistedGrant<DeviceUserCodeGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = tenantId,
            ClientId = openIdClient.ClientId,
            SubjectId = null,
            Payload = new DeviceUserCodeGrant(deviceCode),
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            PersistedGrantService.CreateGrantId(
                tenantId,
                OpenIdConstants.PersistedGrantTypes.DeviceUserCode,
                userCodeNormalized
            ),
            userCodeGrant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        var verificationUri = LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.DeviceVerification,
            values: null
        );

        if (string.IsNullOrEmpty(verificationUri))
            throw new InvalidOperationException("Unable to determine the device verification URI.");

        var verificationUriComplete = LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.DeviceVerification,
            new { user_code = userCodeDisplay }
        );

        var result = new DeviceAuthorizationResult
        {
            DeviceCode = deviceCode,
            UserCode = userCodeDisplay,
            VerificationUri = verificationUri,
            VerificationUriComplete = verificationUriComplete,
            ExpiresIn = (int)lifetime.TotalSeconds,
            Interval = (int)interval.TotalSeconds,
        };

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }

    private static string[] ParseScopes(IFormCollection formData)
    {
        if (!formData.TryGetValue(OpenIdConstants.Parameters.Scope, out var scopeValues))
            return [];

        var scope = scopeValues.ToString();
        if (string.IsNullOrEmpty(scope))
            return [];

        return scope.Split(
            OpenIdConstants.ParameterSeparatorChar,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
    }
}
