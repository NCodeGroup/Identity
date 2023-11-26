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

using Microsoft.AspNetCore.Routing;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization.Messages;

namespace NIdentity.OpenId.Logic.Authorization;

/// <summary>
/// Provides a default implementation for the <see cref="IAuthorizationCallbackService"/> abstraction.
/// </summary>
public class DefaultAuthorizationCallbackService : IAuthorizationCallbackService
{
    private LinkGenerator LinkGenerator { get; }
    private ICryptoService CryptoService { get; }
    private IPersistedGrantService PersistedGrantService { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationCallbackService"/> class.
    /// </summary>
    public DefaultAuthorizationCallbackService(
        LinkGenerator linkGenerator,
        ICryptoService cryptoService,
        IPersistedGrantService persistedGrantService)
    {
        LinkGenerator = linkGenerator;
        CryptoService = cryptoService;
        PersistedGrantService = persistedGrantService;
    }

    /// <inheritdoc />
    public async ValueTask<string> GetContinueUrlAsync(
        AuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationContext.OpenIdContext;
        var clientSettings = authorizationContext.ClientSettings;
        var jsonSerializerOptions = openIdContext.OpenIdServer.JsonSerializerOptions;
        var httpContext = openIdContext.HttpContext;

        var grantKey = CryptoService.GenerateUrlSafeKey();

        var continueUrl = LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.Continue,
            new { state = grantKey });

        if (string.IsNullOrEmpty(continueUrl))
            throw new InvalidOperationException("Unable to determine continue url.");

        await PersistedGrantService.AddAsync(
            openIdContext.OpenIdTenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.ContinueAuthorization,
            grantKey,
            authorizationRequest.ClientId,
            subjectId: null,
            clientSettings.ContinueAuthorizationTimeout,
            authorizationRequest,
            jsonSerializerOptions,
            cancellationToken);

        return continueUrl;
    }

    /// <inheritdoc />
    public async ValueTask<IAuthorizationRequest?> TryGetAuthorizationRequestAsync(
        OpenIdContext openIdContext,
        string state,
        CancellationToken cancellationToken)
    {
        var authorizationRequest = await PersistedGrantService.TryGetAsync<IAuthorizationRequest>(
            openIdContext.OpenIdTenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.ContinueAuthorization,
            grantKey: state,
            singleUse: true,
            setConsumed: true,
            openIdContext.OpenIdServer.JsonSerializerOptions,
            cancellationToken);

        return authorizationRequest;
    }
}
