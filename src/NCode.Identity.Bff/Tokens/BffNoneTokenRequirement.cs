// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>none</c>, which serves no access token and only requires that the
/// request has a session.
/// </summary>
internal class BffNoneTokenRequirement(BffSessionResolver sessionResolver) : IBffTokenRequirement
{
    private readonly BffSessionResolver _sessionResolver = sessionResolver;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.None;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        var state = await _sessionResolver.ResolveAsync(httpContext, cancellationToken);
        return state is null
            ? BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired)
            : BffTokenResult.Success(null);
    }
}
