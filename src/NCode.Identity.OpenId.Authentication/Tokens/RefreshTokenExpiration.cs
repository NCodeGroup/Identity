// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Computes when refresh tokens expire. A rotation family expires <c>refresh_token_lifetime</c> after the grant that
/// began it, however often its tokens are used or rotated; under the <c>absolute</c> policy every token of the family
/// expires then, and under the <c>sliding</c> policy a token expires <c>refresh_token_sliding_lifetime</c> after it was
/// issued or last used, never later than its family.
/// </summary>
// See ADR-1297.
internal static class RefreshTokenExpiration
{
    /// <summary>
    /// Gets when a rotation family expires.
    /// </summary>
    /// <param name="settings">The client's effective settings.</param>
    /// <param name="lineageCreatedWhen">When the family began.</param>
    /// <returns>When the family expires.</returns>
    public static DateTimeOffset GetLineageExpiresWhen(
        IReadOnlySettingCollection settings,
        DateTimeOffset lineageCreatedWhen
    ) => lineageCreatedWhen + settings.GetValue(OpenIdSettingKeys.RefreshTokenLifetime);

    /// <summary>
    /// Determines whether a rotation family has passed its horizon.
    /// </summary>
    /// <param name="settings">The client's effective settings.</param>
    /// <param name="lineageCreatedWhen">When the family began.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when the family expires at or before <paramref name="now"/>.</returns>
    public static bool HasLineageExpired(
        IReadOnlySettingCollection settings,
        DateTimeOffset lineageCreatedWhen,
        DateTimeOffset now
    ) => GetLineageExpiresWhen(settings, lineageCreatedWhen) <= now;

    /// <summary>
    /// Gets when a refresh token that is issued or used at <paramref name="usedWhen"/> expires.
    /// </summary>
    /// <param name="settings">The client's effective settings.</param>
    /// <param name="lineageExpiresWhen">When the token's family expires.</param>
    /// <param name="usedWhen">When the token is issued or used.</param>
    /// <returns>When the token expires, never later than <paramref name="lineageExpiresWhen"/>.</returns>
    public static DateTimeOffset GetTokenExpiresWhen(
        IReadOnlySettingCollection settings,
        DateTimeOffset lineageExpiresWhen,
        DateTimeOffset usedWhen
    )
    {
        var policy = settings.GetValue(OpenIdSettingKeys.RefreshTokenExpirationPolicy);
        if (policy != OpenIdConstants.RefreshTokenExpirationPolicy.Sliding)
            return lineageExpiresWhen;

        var slidingExpiresWhen =
            usedWhen + settings.GetValue(OpenIdSettingKeys.RefreshTokenSlidingLifetime);
        return slidingExpiresWhen < lineageExpiresWhen ? slidingExpiresWhen : lineageExpiresWhen;
    }
}
