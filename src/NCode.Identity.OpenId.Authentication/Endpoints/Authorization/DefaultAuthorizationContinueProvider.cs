// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueProvider"/> abstraction for handling authorization continuations.
/// </summary>
internal class DefaultAuthorizationContinueProvider(
    IOpenIdClientResolver clientResolver,
    IAuthorizationRequestProcessor authorizationRequestProcessor,
    IAuthorizationResponseDeliveryResolver deliveryResolver
) : IContinueProvider
{
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
    private IAuthorizationRequestProcessor AuthorizationRequestProcessor { get; } =
        authorizationRequestProcessor;
    private IAuthorizationResponseDeliveryResolver DeliveryResolver { get; } = deliveryResolver;

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.Authorization;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    )
    {
        var (authorizationRequest, openIdClient) = await ResolveRequestAndClientAsync(
            openIdContext,
            continuePayloadJson,
            cancellationToken
        );
        if (openIdClient is null)
            return InvalidClient(openIdContext);

        authorizationRequest.IsContinuation = true;

        // The parked request's redirect_uri was checked against the client's registration when it arrived.
        var delivery = await DeliveryResolver.ResolveAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authorizationRequest.RedirectUri,
            cancellationToken
        );

        var disposition = await AuthorizationRequestProcessor.ProcessRequestAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            delivery,
            cancellationToken
        );
        return disposition;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> RefuseAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        IOpenIdError error,
        CancellationToken cancellationToken
    )
    {
        var (authorizationRequest, openIdClient) = await ResolveRequestAndClientAsync(
            openIdContext,
            continuePayloadJson,
            cancellationToken
        );
        if (openIdClient is null)
            return InvalidClient(openIdContext);

        authorizationRequest.IsContinuation = true;

        // The parked request's redirect_uri was checked against the client's registration when it arrived.
        var delivery = await DeliveryResolver.ResolveAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authorizationRequest.RedirectUri,
            cancellationToken
        );

        // The refusal is answered to the client at its redirect URI, echoing the state the request carried.
        var result = new AuthorizationResult(
            openIdContext,
            openIdClient,
            delivery,
            error.WithState(delivery.State)
        );
        return ReadOnlyEndpointDisposition.Handled(result);
    }

    // The consumed-once continuation grant is itself the authorization proof (the original request already
    // identified and validated the client), and the credential-less resumption request carries nothing to
    // authenticate. Resolve the client the continuation captured rather than re-authenticating it.
    private async ValueTask<(
        IAuthorizationRequest AuthorizationRequest,
        OpenIdClient? OpenIdClient
    )> ResolveRequestAndClientAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        var authorizationRequest = continuePayloadJson.Deserialize<IAuthorizationRequest>(
            openIdEnvironment.JsonSerializerOptions
        );
        if (authorizationRequest == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        var openIdClient = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            authorizationRequest.ClientId,
            cancellationToken
        );

        return (authorizationRequest, openIdClient);
    }

    private static ReadOnlyEndpointDisposition InvalidClient(OpenIdContext openIdContext)
    {
        var error = openIdContext.ErrorFactory.InvalidClient();
        error.StatusCode = StatusCodes.Status400BadRequest;
        return ReadOnlyEndpointDisposition.Handled(error.AsHttpResult());
    }
}
