// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using NCode.Identity.Jose;
using NCode.Json;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="TokenTypeRequirement"/> by asserting the <c>typ</c> JOSE header matches an allowed media type.
/// </summary>
internal sealed class TokenTypeRequirementHandler : JwtValidationHandler<TokenTypeRequirement>
{
    /// <inheritdoc />
    protected override ValueTask HandleRequirementAsync(
        JwtValidationContext context,
        TokenTypeRequirement requirement,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Normalize the allowable media types once: a frozen set gives O(1) case-insensitive lookups.
        var validTokenTypeSet = requirement
            .ValidTokenTypes.Select(JoseTokenTypes.Normalize)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        var header = context.DecodedJwt.Header;
        if (
            !header.TryGetPropertyValue<string>(JoseClaimNames.Header.Typ, out var tokenType)
            || string.IsNullOrEmpty(tokenType)
        )
        {
            context.Fail(
                JwtValidationFailureCodes.InvalidTokenType,
                "The 'typ' header parameter is missing."
            );
            return ValueTask.CompletedTask;
        }

        if (!validTokenTypeSet.Contains(JoseTokenTypes.Normalize(tokenType)))
        {
            context.Fail(
                JwtValidationFailureCodes.InvalidTokenType,
                $"The 'typ' header parameter '{tokenType}' is invalid because it does not match any of the expected token types."
            );
            return ValueTask.CompletedTask;
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
