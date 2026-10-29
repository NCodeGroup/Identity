#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Computes when refresh tokens expire. A rotation family expires <c>refresh_token_lifetime</c> after the grant that
/// began it, however often its tokens are used or rotated; under the <c>absolute</c> policy every token of the family
/// expires then, and under the <c>sliding</c> policy a token expires <c>refresh_token_sliding_lifetime</c> after it was
/// issued or last used, never later than its family.
/// </summary>
// See ADR-0297.
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
