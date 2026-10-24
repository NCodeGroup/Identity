#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="IClientAuthenticationService"/> abstraction. Every registered
/// <see cref="IClientAuthenticationHandler"/> examines the request; the service selects the client they authenticated
/// and accepts it only when the method it authenticated with is the one it is registered to use and its ceiling permits
/// that method, whichever handler produced it. An authenticated confidential client is then judged at the
/// <c>client_authentication</c> rate-limit stage, once per request, and refused with <c>429 Too Many Requests</c> when
/// it is over a limit. The constructor throws an <see cref="InvalidOperationException"/> unless every registered
/// handler's method has a <see cref="ClientAuthenticationMethodDescriptor"/>, no two handlers authenticate one method,
/// and every described method has a handler.
/// </summary>
// See ADR-0145, ADR-0196, ADR-0207 and ADR-0246.
internal class DefaultClientAuthenticationService(
    IEnumerable<IClientAuthenticationHandler> handlers,
    IClientAuthenticationMethodCatalog clientAuthenticationMethodCatalog,
    IAuditEventRecorder auditEventRecorder,
    IRequestRateLimiter rateLimiter,
    ILogger<DefaultClientAuthenticationService> logger
) : IClientAuthenticationService
{
    // Per-request memoization key. The result is cached on the HttpContext so it is scoped to the current request;
    // this service is a singleton, so an instance field would leak one request's result to every other request.
    private static readonly object CacheKey = new();

    private ImmutableArray<IClientAuthenticationHandler> Handlers { get; } =
        EnsureEveryMethodIsDescribed([.. handlers], clientAuthenticationMethodCatalog);

    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    private IRequestRateLimiter RateLimiter { get; } = rateLimiter;

    private ILogger<DefaultClientAuthenticationService> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var items = openIdContext.Http.Items;
        if (
            items.TryGetValue(CacheKey, out var cached)
            && cached is ClientAuthenticationResult result
        )
        {
            return result;
        }

        var authenticationResult = await AuthenticateCoreAsync(openIdContext, cancellationToken);

        // Counted on the uncached path only, so a request that authenticates its client more than once counts once. Only
        // a confidential client proves its identity; a public client is named by its client_id alone, so counting it
        // would let anyone spend its allowance and refuse its real users.
        if (authenticationResult.ConfidentialClient is { } client)
        {
            var rateLimitContext = new RateLimitContext
            {
                OpenIdContext = openIdContext,
                Client = client,
            };
            var decision = await RateLimiter.AcquireAsync(
                RateLimitStages.ClientAuthentication,
                rateLimitContext,
                cancellationToken
            );
            if (decision.RetryAfter is { } retryAfter)
                authenticationResult = new ClientAuthenticationResult(
                    RateLimitErrors.Refuse(openIdContext, retryAfter)
                );
        }

        items[CacheKey] = authenticationResult;
        return authenticationResult;
    }

    private async ValueTask<ClientAuthenticationResult> AuthenticateCoreAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        // Requirements:
        // - Error if multiple results with different client IDs
        // - Error if multiple confidential clients
        // - Return first confidential client, if any
        // - Otherwise return first public client

        var results = new List<ClientAuthenticationResult>(Handlers.Length);

        foreach (var handler in Handlers)
        {
            var result = await handler.AuthenticateClientAsync(openIdContext, cancellationToken);

            if (result.IsUndefined)
                continue;

            if (result.IsError)
                return result;

            Debug.Assert(result.HasClient);

            if (
                results.Count > 0
                && results[0].Client is { } firstClient
                && !string.Equals(
                    result.Client.ClientId,
                    firstClient.ClientId,
                    StringComparison.Ordinal
                )
            )
            {
                return new ClientAuthenticationResult(
                    errorFactory
                        .InvalidRequest("Multiple client authentication methods were provided.")
                        .WithStatusCode(StatusCodes.Status400BadRequest)
                );
            }

            results.Add(result);
        }

        if (results.Count == 0)
            return ClientAuthenticationResult.Undefined;

        var confidentialResults = results.Where(result => result.IsConfidential).ToList();

        if (confidentialResults.Count > 1)
            return new ClientAuthenticationResult(
                errorFactory
                    .InvalidRequest("Multiple client authentication methods were provided.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );

        var selected = confidentialResults.Count == 1 ? confidentialResults[0] : results[0];

        return await EnforceRegisteredAuthenticationMethodAsync(
            openIdContext,
            selected,
            cancellationToken
        );
    }

    // Accepts the client only when the method it authenticated with is its registered token_endpoint_auth_method and its
    // token_endpoint_auth_methods_supported ceiling permits that method, so a client never authenticates as a type it is
    // not registered as (e.g. a confidential client presenting only its client_id).
    private async ValueTask<ClientAuthenticationResult> EnforceRegisteredAuthenticationMethodAsync(
        OpenIdContext openIdContext,
        ClientAuthenticationResult result,
        CancellationToken cancellationToken
    )
    {
        if (result.Client is not { } client)
            return result;

        var method = result.IsConfidential
            ? result.ConfidentialClient.AuthenticationMethod
            : OpenIdConstants.ClientAuthenticationMethods.None;

        if (client.MayAuthenticateWith(method))
            return result;

        string reason;
        var registeredMethod = client.RegisteredAuthenticationMethod;
        if (string.Equals(method, registeredMethod, StringComparison.Ordinal))
        {
            Logger.ClientAuthenticationMethodNotPermitted(client.ClientId, registeredMethod);
            reason = "authentication_method_not_permitted";
        }
        else
        {
            Logger.ClientAuthenticationMethodNotRegistered(
                client.ClientId,
                method,
                registeredMethod
            );
            reason = "authentication_method_not_registered";
        }

        await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
            openIdContext,
            client.ClientId,
            method,
            reason,
            cancellationToken
        );

        return new ClientAuthenticationResult(
            openIdContext
                .ErrorFactory.InvalidClient()
                .WithDescription(
                    "The client authentication method is not permitted for this client."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
        );
    }

    /// <summary>
    /// Ensures that the handlers and the method descriptors correspond one to one. A handler whose method has no
    /// descriptor is not advertised, two handlers that authenticate one method are ambiguous, and a descriptor without
    /// a handler is advertised but cannot authenticate.
    /// </summary>
    /// <param name="handlers">The registered client-authentication handlers.</param>
    /// <param name="catalog">The catalog of the registered client-authentication method descriptors.</param>
    /// <returns>The <paramref name="handlers"/>, unchanged.</returns>
    /// <exception cref="InvalidOperationException">
    /// A handler's method has no descriptor, two handlers authenticate one method, or a descriptor has no handler.
    /// </exception>
    // See ADR-0207.
    private static ImmutableArray<IClientAuthenticationHandler> EnsureEveryMethodIsDescribed(
        ImmutableArray<IClientAuthenticationHandler> handlers,
        IClientAuthenticationMethodCatalog catalog
    )
    {
        var handledMethods = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handler in handlers)
        {
            var method = handler.AuthenticationMethod;

            if (!catalog.TryGet(method, out _))
                throw new InvalidOperationException(
                    $"The client-authentication handler '{handler.GetType().Name}' authenticates '{method}', which no client-authentication method descriptor names."
                );

            if (!handledMethods.Add(method))
                throw new InvalidOperationException(
                    $"More than one client-authentication handler authenticates '{method}'."
                );
        }

        foreach (var name in catalog.MethodNames)
        {
            if (!handledMethods.Contains(name))
                throw new InvalidOperationException(
                    $"No client-authentication handler authenticates the registered method '{name}'."
                );
        }

        return handlers;
    }
}
