// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Endpoints;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffTokenAccessor"/>, which dispatches to the registered <see cref="IBffTokenRequirement"/>
/// that the request names.
/// </summary>
internal class DefaultBffTokenAccessor : IBffTokenAccessor
{
    private FrozenDictionary<string, IBffTokenRequirement> Requirements { get; }
    private BffTokenContexts Contexts { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBffTokenAccessor"/> class.
    /// </summary>
    /// <param name="requirements">The registered token requirements.</param>
    /// <param name="contexts">The factory of the token contexts of requests.</param>
    public DefaultBffTokenAccessor(
        IEnumerable<IBffTokenRequirement> requirements,
        BffTokenContexts contexts
    )
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(contexts);

        Contexts = contexts;

        var map = new Dictionary<string, IBffTokenRequirement>(StringComparer.Ordinal);
        foreach (var requirement in requirements)
        {
            if (!map.TryAdd(requirement.Name, requirement))
                throw new InvalidOperationException(
                    $"The token requirement '{requirement.Name}' is registered more than once."
                );
        }

        Requirements = map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        ThrowIfNotGuarded(httpContext);

        var context = Contexts.ForRequest(httpContext);
        return DispatchAsync(context, request, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var httpContext = context.HttpContext;
        if (httpContext is not null)
            ThrowIfNotGuarded(httpContext);

        return DispatchAsync(context, request, cancellationToken);
    }

    private static void ThrowIfNotGuarded(HttpContext httpContext)
    {
        if (!BffGuardedEndpoints.IsGuarded(httpContext.GetEndpoint()))
            throw new InvalidOperationException(
                "A BFF access token is available only to an endpoint the BFF guards. Mark an API AsBffApi() or [BffApi], a hub AsBffHub() or [BffHub], or a page AsBffPage() or [BffPage]."
            );
    }

    private ValueTask<BffTokenResult> DispatchAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!Requirements.TryGetValue(request.Requirement, out var requirement))
            throw new InvalidOperationException(
                $"The token requirement '{request.Requirement}' is not registered."
            );

        return requirement.GetAccessTokenAsync(context, request, cancellationToken);
    }
}
