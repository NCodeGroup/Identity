// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>user</c>, which serves the access token of the signed-in user.
/// </summary>
internal class BffUserTokenRequirement(IBffUserTokenService service) : IBffTokenRequirement
{
    private readonly IBffUserTokenService _service = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.User;

    /// <inheritdoc />
    public ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        return _service.GetAccessTokenAsync(
            httpContext,
            request.Resource,
            request.Scope,
            cancellationToken
        );
    }
}
