#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Json;
using NCode.Time;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="TokenLifetimeRequirement"/> by asserting the <c>nbf</c>, <c>iat</c>, and <c>exp</c> payload claims
/// are valid. Each claim, when present, must be an RFC 7519 <c>NumericDate</c>; a present claim that is not fails
/// validation rather than being treated as absent.
/// </summary>
internal sealed class TokenLifetimeRequirementHandler
    : JwtValidationHandler<TokenLifetimeRequirement>
{
    /// <inheritdoc />
    protected override ValueTask HandleRequirementAsync(
        JwtValidationContext context,
        TokenLifetimeRequirement requirement,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGetFailureReason(context, requirement, out var failureReason))
            context.Fail(failureReason);
        else
            context.Succeed(requirement);

        return ValueTask.CompletedTask;
    }

    private static bool TryGetFailureReason(
        JwtValidationContext context,
        TokenLifetimeRequirement requirement,
        [NotNullWhen(true)] out string? failureReason
    )
    {
        var payload = context.DecodedJwt.Payload;
        if (!TryGetOptionalNumericDate(payload, JoseClaimNames.Payload.Nbf, out var notBefore))
        {
            failureReason = "The token's 'nbf' claim is not a valid NumericDate.";
            return true;
        }

        if (!TryGetOptionalNumericDate(payload, JoseClaimNames.Payload.Iat, out var issuedAt))
        {
            failureReason = "The token's 'iat' claim is not a valid NumericDate.";
            return true;
        }

        if (!TryGetOptionalNumericDate(payload, JoseClaimNames.Payload.Exp, out var expires))
        {
            failureReason = "The token's 'exp' claim is not a valid NumericDate.";
            return true;
        }

        if (notBefore is null && requirement.RequireNotBefore)
        {
            failureReason = "The token is missing a not-before time.";
            return true;
        }

        if (issuedAt is null && requirement.RequireIssuedAt)
        {
            failureReason = "The token is missing an issued-at time.";
            return true;
        }

        // A lifetime cap cannot hold for a token that never expires.
        var requireExpirationTime =
            requirement.RequireExpirationTime || requirement.MaxLifetime.HasValue;
        if (expires is null && requireExpirationTime)
        {
            failureReason = "The token is missing an expiration time.";
            return true;
        }

        if (notBefore > expires)
        {
            failureReason =
                $"The NotBefore value '{notBefore:O}' cannot be after the Expires value '{expires:O}'.";
            return true;
        }

        var nowTicks = context.TimeProvider.GetTimestampWithPrecisionInSeconds();
        var clockSkewTicks = requirement.ClockSkew.Ticks;
        var latestStartTicks = nowTicks + clockSkewTicks;

        if (notBefore?.UtcTicks > latestStartTicks)
        {
            failureReason = $"The token is not valid before '{notBefore:O}'.";
            return true;
        }

        if (issuedAt?.UtcTicks > latestStartTicks)
        {
            failureReason = $"The token was issued in the future at '{issuedAt:O}'.";
            return true;
        }

        if (expires?.UtcTicks < nowTicks - clockSkewTicks)
        {
            failureReason = $"The token is not valid after '{expires:O}'.";
            return true;
        }

        if (requirement.MaxLifetime is { } maxLifetime && expires is { } expiresAt)
        {
            // Measured from the earliest asserted start; a token asserting none starts at latestStartTicks.
            var startTicks = Math.Min(
                issuedAt?.UtcTicks ?? latestStartTicks,
                notBefore?.UtcTicks ?? latestStartTicks
            );
            if (expiresAt.UtcTicks - startTicks > maxLifetime.Ticks)
            {
                failureReason = $"The token's lifetime exceeds the maximum of '{maxLifetime:c}'.";
                return true;
            }
        }

        failureReason = null;
        return false;
    }

    // An absent claim yields null; a present claim must be a valid NumericDate or the read fails.
    private static bool TryGetOptionalNumericDate(
        JsonElement payload,
        string claimName,
        out DateTimeOffset? value
    )
    {
        if (!payload.TryGetProperty(claimName, out var element))
        {
            value = null;
            return true;
        }

        if (element.TryGetNumericDate(out var instant))
        {
            value = instant;
            return true;
        }

        value = null;
        return false;
    }
}
