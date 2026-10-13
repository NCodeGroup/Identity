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

using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="CreateDeviceAuthorizationRequestCommand"/> handler that
/// generates and persists the <c>device_code</c> and <c>user_code</c> grants, and returns the
/// <see cref="DeviceAuthorizationResult"/> (RFC 8628 §3.2).
/// </summary>
internal class DefaultCreateDeviceAuthorizationRequestHandler(
    TimeProvider timeProvider,
    LinkGenerator linkGenerator,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService,
    IUserCodeGenerator userCodeGenerator
) : ICommandResponseHandler<CreateDeviceAuthorizationRequestCommand, DeviceAuthorizationResult>
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IUserCodeGenerator UserCodeGenerator { get; } = userCodeGenerator;

    /// <inheritdoc />
    public async ValueTask<DeviceAuthorizationResult> HandleAsync(
        CreateDeviceAuthorizationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, request) = command;
        var httpContext = openIdContext.Http;
        var tenantId = openIdContext.Tenant.TenantId;
        var settings = openIdClient.Settings;

        var scopes = request.Scopes ?? [];

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
            SessionId = null,
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
            SessionId = null,
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

        return new DeviceAuthorizationResult
        {
            DeviceCode = deviceCode,
            UserCode = userCodeDisplay,
            VerificationUri = verificationUri,
            VerificationUriComplete = verificationUriComplete,
            ExpiresIn = (int)lifetime.TotalSeconds,
            Interval = (int)interval.TotalSeconds,
        };
    }
}
