// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>user</c>, which serves the access token of the signed-in user.
/// </summary>
internal class BffUserTokenRequirement(IBffUserTokenService service) : IBffTokenRequirement
{
    private IBffUserTokenService Service { get; } = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.User;

    /// <inheritdoc />
    public ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var scope = IBffSessionScope.From(context);
        return Service.GetAccessTokenAsync(scope, request, cancellationToken);
    }
}
