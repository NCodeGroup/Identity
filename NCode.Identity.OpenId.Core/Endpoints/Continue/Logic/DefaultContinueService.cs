﻿#region Copyright Preamble

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
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Models;

namespace NCode.Identity.OpenId.Endpoints.Continue.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueService"/> abstraction.
/// </summary>
public class DefaultContinueService(
    TimeProvider timeProvider,
    LinkGenerator linkGenerator,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService
) : IContinueService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
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
        var openIdEnvironment = openIdContext.Environment;
        var httpContext = openIdContext.Http;

        // the continue endpoint has a single 'state' query string parameter that contains a one-time use grant key
        var grantKey = CryptoService.GenerateUrlSafeKey();
        var continueUrl = LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.Continue,
            new { state = grantKey });

        if (string.IsNullOrEmpty(continueUrl))
            throw new InvalidOperationException("Unable to determine continue url.");

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var payloadJson = JsonSerializer.SerializeToElement(
            payload,
            openIdEnvironment.JsonSerializerOptions
        );

        var continueEnvelope = new ContinueEnvelope
        {
            Code = continueCode,
            PayloadJson = payloadJson
        };

        var persistedGrantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.Continue,
            GrantKey = grantKey
        };

        var persistedGrant = new PersistedGrant<ContinueEnvelope>
        {
            ClientId = clientId,
            SubjectId = subjectId,
            Payload = continueEnvelope
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            persistedGrantId,
            persistedGrant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        return continueUrl;
    }
}
