#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
