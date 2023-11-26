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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints.Continue;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the continue endpoint.
/// </summary>
public class DefaultContinueEndpointHandler(
    IOpenIdContextFactory contextFactory,
    IPersistedGrantService persistedGrantService,
    IContinueProviderSelector continueProviderSelector
) : IOpenIdEndpointProvider
{
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
        .OpenIdDiscoverable(false);

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(state))
        {
            // TODO: more details
            return TypedResults.BadRequest();
        }

        var openIdContext = await ContextFactory.CreateContextAsync(
            httpContext,
            mediator,
            cancellationToken);

        var continueEnvelope = await PersistedGrantService.TryGetAsync<ContinueEnvelope>(
            openIdContext.OpenIdTenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            grantKey: state,
            singleUse: true,
            setConsumed: true,
            cancellationToken);

        if (continueEnvelope is null)
        {
            // TODO: more details
            return TypedResults.BadRequest();
        }

        var provider = ContinueProviderSelector.SelectProvider(continueEnvelope.Code);
        var result = await provider.ContinueAsync(openIdContext, continueEnvelope.Payload, cancellationToken);

        return result;
    }
}
