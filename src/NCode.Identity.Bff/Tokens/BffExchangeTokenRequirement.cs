// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>exchange</c>, which serves an access token that the provider issues
/// for the user's access token through token exchange (RFC 8693).
/// </summary>
internal class BffExchangeTokenRequirement(IBffExchangeTokenService service) : IBffTokenRequirement
{
    private IBffExchangeTokenService Service { get; } = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.Exchange;

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
