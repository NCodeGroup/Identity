// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the continue endpoint.
/// </summary>
internal class DefaultContinueEndpointHandler(
    ILogger<DefaultContinueEndpointHandler> logger,
    IPersistedGrantService persistedGrantService,
    IContinueProviderSelector continueProviderSelector
) : IEndpointProvider
{
    private ILogger<DefaultContinueEndpointHandler> Logger { get; } = logger;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IContinueProviderSelector ContinueProviderSelector { get; } = continueProviderSelector;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.Continue,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.Continue)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .WithOpenIdDiscoverable(false)
            .WithFrontChannel();

    /// <summary>
    /// Continue a paused authorization flow.
    /// </summary>
    /// <remarks>
    /// Resumes an authorization request that was interrupted for user interaction such as login or consent. The
    /// paused flow is identified by the opaque <c>state</c> value issued when it was suspended. This is an internal
    /// redirect target used by the authorization server, not a public protocol endpoint.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="state">The opaque value that identifies the paused authorization flow to resume.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The authorization flow was resumed.</response>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromQuery] string? state,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(state))
        {
            Logger.MissingStateParameter();
            return TypedResults.BadRequest();
        }

        var openIdContext = httpContext.GetOpenIdContext();

        var tenantId = openIdContext.Tenant.TenantId;

        var persistedGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            state
        );

        var persistedGrantOrNull =
            await PersistedGrantService.ConsumeOnceOrDefault<ContinueEnvelope>(
                openIdContext,
                persistedGrantId,
                cancellationToken
            );

        if (!persistedGrantOrNull.HasValue)
        {
            Logger.InvalidStateParameter();
            return TypedResults.BadRequest();
        }

        var persistedGrant = persistedGrantOrNull.Value;
        Debug.Assert(persistedGrant.Status == PersistedGrantStatus.Active);

        var continueEnvelope = persistedGrant.Payload;
        var provider = ContinueProviderSelector.SelectProvider(continueEnvelope.Code);
        var disposition = await provider.ContinueAsync(
            openIdContext,
            continueEnvelope.PayloadJson,
            cancellationToken
        );

        if (disposition.HasHttpResult)
        {
            return disposition.HttpResult;
        }

        if (disposition.WasHandled)
        {
            return EmptyHttpResult.Instance;
        }

        Logger.ContinueProviderNotHandled();
        return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
    }
}
