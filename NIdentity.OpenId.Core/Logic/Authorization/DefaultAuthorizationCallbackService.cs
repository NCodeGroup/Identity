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

using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Continue;
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Logic.Authorization;

/// <summary>
/// Provides a default implementation for the <see cref="IAuthorizationCallbackService"/> abstraction.
/// </summary>
public class DefaultAuthorizationCallbackService : IAuthorizationCallbackService
{
    private LinkGenerator LinkGenerator { get; }
    private ICryptoService CryptoService { get; }
    private IPersistedGrantService PersistedGrantService { get; }
    private OpenIdServer OpenIdServer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationCallbackService"/> class.
    /// </summary>
    public DefaultAuthorizationCallbackService(
        LinkGenerator linkGenerator,
        ICryptoService cryptoService,
        IPersistedGrantService persistedGrantService,
        OpenIdServer openIdServer)
    {
        LinkGenerator = linkGenerator;
        CryptoService = cryptoService;
        PersistedGrantService = persistedGrantService;
        OpenIdServer = openIdServer;
    }

    /// <inheritdoc />
    public async ValueTask<string> GetContinueUrlAsync(
        AuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationContext.OpenIdContext;
        var clientSettings = authorizationContext.ClientSettings;
        var httpContext = openIdContext.HttpContext;

        var grantKey = CryptoService.GenerateUrlSafeKey();

        var continueUrl = LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.Continue,
            new { state = grantKey });

        if (string.IsNullOrEmpty(continueUrl))
            throw new InvalidOperationException("Unable to determine continue url.");

        var continuePayload = JsonSerializer.SerializeToElement(
            authorizationRequest,
            OpenIdServer.JsonSerializerOptions);

        var continueEnvelope = new ContinueEnvelope
        {
            Code = OpenIdConstants.ContinueCodes.Authorization,
            Payload = continuePayload
        };

        await PersistedGrantService.AddAsync(
            openIdContext.OpenIdTenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            grantKey,
            authorizationRequest.ClientId,
            subjectId: null,
            clientSettings.ContinueAuthorizationTimeout,
            payload: continueEnvelope,
            cancellationToken);

        return continueUrl;
    }
}
