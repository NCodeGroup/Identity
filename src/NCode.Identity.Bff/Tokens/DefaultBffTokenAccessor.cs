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

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBffTokenAccessor"/> class.
    /// </summary>
    /// <param name="requirements">The registered token requirements.</param>
    public DefaultBffTokenAccessor(IEnumerable<IBffTokenRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

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

        if (!BffGuardedEndpoints.IsGuarded(httpContext.GetEndpoint()))
            throw new InvalidOperationException(
                "A BFF access token is available only to an endpoint the BFF guards. Mark an API AsBffApi() or [BffApi], or a page AsBffPage() or [BffPage]."
            );

        if (!_requirements.TryGetValue(request.Requirement, out var requirement))
            throw new InvalidOperationException(
                $"The token requirement '{request.Requirement}' is not registered."
            );

        return requirement.GetAccessTokenAsync(httpContext, request, cancellationToken);
    }
}
