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
            context.Fail("The 'typ' header parameter is missing.");
            return ValueTask.CompletedTask;
        }

        if (!validTokenTypeSet.Contains(JoseTokenTypes.Normalize(tokenType)))
        {
            context.Fail(
                $"The 'typ' header parameter '{tokenType}' is invalid because it does not match any of the expected token types."
            );
            return ValueTask.CompletedTask;
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
