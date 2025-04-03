#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Endpoints.Api;

[PublicAPI]
public abstract class BaseApiEndpointHandler
{
    protected abstract IAuthorizationService AuthorizationService { get; }

    protected internal virtual T? ToStateResponse<T>(ConcurrentState<T>? stateOrNull)
    {
        return stateOrNull.HasValue ? stateOrNull.Value.Value : default;
    }

    protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue>(
        HttpContext httpContext,
        TValue? valueOrNull,
        string authorizationPolicyName
    )
    {
        return await ProcessGetAsync(httpContext, valueOrNull, authorizationPolicyName, value => value);
    }

    protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        TValue? valueOrNull,
        string authorizationPolicyName,
        Func<TValue, TResponse> mapper
    )
    {
        if (valueOrNull is null)
        {
            return TypedResults.NotFound();
        }

        // https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-9.0
        var user = httpContext.User;
        var authorizationResult = await AuthorizationService.AuthorizeAsync(user, valueOrNull, authorizationPolicyName);

        if (authorizationResult.Succeeded)
        {
            if (valueOrNull is ISupportConcurrencyToken supportConcurrencyToken)
            {
                var concurrencyToken = supportConcurrencyToken.ConcurrencyToken;
                httpContext.Response.Headers.ETag = concurrencyToken;

                var ifNoneMatch = httpContext.Request.Headers.IfNoneMatch;
                if (string.Equals(ifNoneMatch, concurrencyToken, StringComparison.Ordinal))
                {
                    return TypedResults.StatusCode(StatusCodes.Status304NotModified);
                }
            }

            var response = mapper(valueOrNull);
            return TypedResults.Json(response);
        }

        if (user.Identity?.IsAuthenticated ?? false)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Unauthorized();
    }
}
