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
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Endpoints.Continue.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueService"/> abstraction.
/// </summary>
public class DefaultContinueService(
    OpenIdServer openIdServer,
    LinkGenerator linkGenerator,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService
) : IContinueService
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public async ValueTask<string> GetContinueUrlAsync<TPayload>(
        OpenIdContext openIdContext,
        string continueCode,
        string? clientId,
        string? subjectId,
        TimeSpan lifetime,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var httpContext = openIdContext.HttpContext;
        var tenantId = openIdContext.OpenIdTenant.TenantId;

        var grantKey = CryptoService.GenerateUrlSafeKey();

        var continueUrl = LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.Continue,
            new { state = grantKey });

        if (string.IsNullOrEmpty(continueUrl))
            throw new InvalidOperationException("Unable to determine continue url.");

        var payloadJson = JsonSerializer.SerializeToElement(
            payload,
            OpenIdServer.JsonSerializerOptions);

        var continueEnvelope = new ContinueEnvelope
        {
            Code = continueCode,
            Payload = payloadJson
        };

        await PersistedGrantService.AddAsync(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            grantKey,
            clientId,
            subjectId,
            lifetime,
            payload: continueEnvelope,
            cancellationToken);

        return continueUrl;
    }
}
