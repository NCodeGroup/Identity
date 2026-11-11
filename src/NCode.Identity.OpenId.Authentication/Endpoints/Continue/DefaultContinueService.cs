// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueService"/> abstraction.
/// </summary>
internal class DefaultContinueService(
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
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var tenantId = openIdContext.Tenant.TenantId;
        var httpContext = openIdContext.Http;

        // the continue endpoint has a single 'state' query string parameter that contains a one-time use grant key
        var grantKey = CryptoService.GenerateUrlSafeKey();
        var continueUrl = LinkGenerator.GetTenantUriByName(
            openIdContext,
            OpenIdConstants.EndpointNames.Continue,
            new { state = grantKey }
        );

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
            PayloadJson = payloadJson,
        };

        var persistedGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            grantKey
        );

        var persistedGrant = new PersistedGrant<ContinueEnvelope>
        {
            TenantId = tenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = null,
            Payload = continueEnvelope,
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
