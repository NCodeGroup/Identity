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
    private readonly FrozenDictionary<string, IBffTokenRequirement> _requirements;
    private readonly BffTokenContexts _contexts;

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

        _contexts = contexts;

        var map = new Dictionary<string, IBffTokenRequirement>(StringComparer.Ordinal);
        foreach (var requirement in requirements)
        {
            if (!map.TryAdd(requirement.Name, requirement))
                throw new InvalidOperationException(
                    $"The token requirement '{requirement.Name}' is registered more than once."
                );
        }

        _requirements = map.ToFrozenDictionary(StringComparer.Ordinal);
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

        return DispatchAsync(_contexts.ForRequest(httpContext), request, cancellationToken);
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
        if (!_requirements.TryGetValue(request.Requirement, out var requirement))
            throw new InvalidOperationException(
                $"The token requirement '{request.Requirement}' is not registered."
            );

        return requirement.GetAccessTokenAsync(context, request, cancellationToken);
    }
}
