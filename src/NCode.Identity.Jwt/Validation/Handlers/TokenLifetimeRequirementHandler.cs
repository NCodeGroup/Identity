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

using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Json;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="TokenLifetimeRequirement"/> by asserting the <c>nbf</c> and <c>exp</c> payload claims are valid.
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
        var hasNotBefore = payload.TryGetPropertyValue<DateTime>(
            JoseClaimNames.Payload.Nbf,
            out var notBefore
        );
        var hasExpires = payload.TryGetPropertyValue<DateTime>(
            JoseClaimNames.Payload.Exp,
            out var expires
        );
        var nowTicks = context.TimeProvider.GetTimestampWithPrecisionInSeconds();

        if (!hasExpires && requirement.RequireExpirationTime)
        {
            context.Fail("The token is missing an expiration time.");
            return ValueTask.CompletedTask;
        }

        if (hasNotBefore && hasExpires && notBefore > expires)
        {
            context.Fail(
                $"The NotBefore value '{notBefore:O}' cannot be after the Expires value '{expires:O}'."
            );
            return ValueTask.CompletedTask;
        }

        if (hasNotBefore && notBefore.Ticks > nowTicks + clockSkewTicks)
        {
            context.Fail($"The token is not valid before '{notBefore:O}'.");
            return ValueTask.CompletedTask;
        }

        if (hasExpires && expires.Ticks < nowTicks - clockSkewTicks)
        {
            context.Fail($"The token is not valid after '{expires:O}'.");
            return ValueTask.CompletedTask;
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
