// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>exchange</c>, which serves an access token that the provider issues
/// for the user's access token through token exchange (RFC 8693).
/// </summary>
internal class BffExchangeTokenRequirement(
    IBffExchangeTokenService service,
    BffTokenContexts contexts
) : IBffTokenRequirement
{
    private readonly IBffExchangeTokenService _service = service;
    private readonly BffTokenContexts _contexts = contexts;

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

        var scope = _contexts.ForRequest(httpContext);
        return _service.GetAccessTokenAsync(scope, request, cancellationToken);
    }
}
