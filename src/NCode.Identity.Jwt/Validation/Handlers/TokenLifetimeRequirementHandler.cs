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

using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Json;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="TokenLifetimeRequirement"/> by asserting the <c>nbf</c> and <c>exp</c> payload claims are valid.
/// Each claim, when present, must be an RFC 7519 <c>NumericDate</c>; a present claim that is not fails validation rather
/// than being treated as absent.
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

        var clockSkewTicks = requirement.ClockSkew.Ticks;

        var payload = context.DecodedJwt.Payload;
        if (!TryGetOptionalNumericDate(payload, JoseClaimNames.Payload.Nbf, out var notBefore))
        {
            context.Fail("The token's 'nbf' claim is not a valid NumericDate.");
            return ValueTask.CompletedTask;
        }

        if (!TryGetOptionalNumericDate(payload, JoseClaimNames.Payload.Exp, out var expires))
        {
            context.Fail("The token's 'exp' claim is not a valid NumericDate.");
            return ValueTask.CompletedTask;
        }

        var nowTicks = context.TimeProvider.GetTimestampWithPrecisionInSeconds();

        if (expires is null && requirement.RequireExpirationTime)
        {
            context.Fail("The token is missing an expiration time.");
            return ValueTask.CompletedTask;
        }

        if (notBefore > expires)
        {
            context.Fail(
                $"The NotBefore value '{notBefore:O}' cannot be after the Expires value '{expires:O}'."
            );
            return ValueTask.CompletedTask;
        }

        if (notBefore?.UtcTicks > nowTicks + clockSkewTicks)
        {
            context.Fail($"The token is not valid before '{notBefore:O}'.");
            return ValueTask.CompletedTask;
        }

        if (expires?.UtcTicks < nowTicks - clockSkewTicks)
        {
            context.Fail($"The token is not valid after '{expires:O}'.");
            return ValueTask.CompletedTask;
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
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
