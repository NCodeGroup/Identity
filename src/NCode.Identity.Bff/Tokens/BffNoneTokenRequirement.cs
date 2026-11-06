// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>none</c>, which serves no access token and only requires that the
/// request has a session.
/// </summary>
internal class BffNoneTokenRequirement : IBffTokenRequirement
{
    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.None;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var scope = IBffSessionScope.From(context);
        var state = await scope.ResolveAsync(cancellationToken);
        return state is null
            ? BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            )
            : BffTokenResult.Success(null);
    }
}
