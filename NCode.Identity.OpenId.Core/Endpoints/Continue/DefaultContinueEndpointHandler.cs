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

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Models;

namespace NCode.Identity.OpenId.Endpoints.Continue;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the continue endpoint.
/// </summary>
public class DefaultContinueEndpointHandler(
    ILogger<DefaultContinueEndpointHandler> logger,
    IOpenIdContextFactory contextFactory,
    IPersistedGrantService persistedGrantService,
    IContinueProviderSelector continueProviderSelector
) : IOpenIdEndpointProvider
{
    private ILogger<DefaultContinueEndpointHandler> Logger { get; } = logger;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IContinueProviderSelector ContinueProviderSelector { get; } = continueProviderSelector;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapMethods(
            OpenIdConstants.EndpointPaths.Continue,
            new[] { HttpMethods.Get, HttpMethods.Post },
            HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Continue)
        .WithOpenIdDiscoverable(false);

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(state))
        {
            Logger.LogInformation("Missing 'state' parameter.");
            return TypedResults.BadRequest();
        }

        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken);

        var persistedGrantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.Continue,
            GrantKey = state
        };

        var persistedGrantOrNull = await PersistedGrantService.TryConsumeOnce<ContinueEnvelope>(
            persistedGrantId,
            cancellationToken);

        if (!persistedGrantOrNull.HasValue)
        {
            Logger.LogInformation("Invalid 'state' parameter.");
            return TypedResults.BadRequest();
        }

        var persistedGrant = persistedGrantOrNull.Value;
        Debug.Assert(persistedGrant.Status == PersistedGrantStatus.Active);

        var continueEnvelope = persistedGrant.Payload;
        var provider = ContinueProviderSelector.SelectProvider(continueEnvelope.Code);
        var result = await provider.ContinueAsync(openIdContext, continueEnvelope.Payload, cancellationToken);

        return result;
    }
}
