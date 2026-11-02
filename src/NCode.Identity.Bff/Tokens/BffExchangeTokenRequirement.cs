// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>exchange</c>, which serves an access token that the provider issues
/// for the user's access token through token exchange (RFC 8693).
/// </summary>
internal class BffExchangeTokenRequirement(IBffExchangeTokenService service) : IBffTokenRequirement
{
    private readonly IBffExchangeTokenService _service = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.Exchange;

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
