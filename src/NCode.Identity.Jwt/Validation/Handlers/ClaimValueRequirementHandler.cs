// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Text.Json;
using NCode.Identity.Json;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="ClaimValueRequirement"/> by asserting the configured claim exists with an allowed value.
/// </summary>
internal sealed class ClaimValueRequirementHandler : JwtValidationHandler<ClaimValueRequirement>
{
    /// <inheritdoc />
    protected override ValueTask HandleRequirementAsync(
        JwtValidationContext context,
        ClaimValueRequirement requirement,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var claims = requirement.UsePayload
            ? context.DecodedJwt.Payload
            : context.DecodedJwt.Header;

        if (!claims.TryGetProperty(requirement.ClaimName, out var property))
        {
            context.Fail(
                requirement.FailureCode,
                $"The claim '{requirement.ClaimName}' is missing."
            );
            return ValueTask.CompletedTask;
        }

        if (property.IsNullOrUndefined())
        {
            context.Fail(requirement.FailureCode, $"The claim '{requirement.ClaimName}' is null.");
            return ValueTask.CompletedTask;
        }

        // Snapshot the allowable values once: a frozen set gives O(1) ordinal lookups.
        var validValueSet = requirement.ValidValues.ToFrozenSet(StringComparer.Ordinal);

        if (property.ValueKind == JsonValueKind.Array)
        {
            if (!requirement.AllowCollection)
            {
                context.Fail(
                    requirement.FailureCode,
                    $"The claim '{requirement.ClaimName}' does not allow multiple values."
                );
                return ValueTask.CompletedTask;
            }

            // A multi-valued claim is valid when it contains at least one of the valid values; extra values are
            // permitted (RFC 7519 §4.1.3 audience semantics).
            if (
                !property
                    .EnumerateArray()
                    .Any(jsonElement => validValueSet.Contains(jsonElement.ToString()))
            )
            {
                context.Fail(
                    requirement.FailureCode,
                    $"The claim '{requirement.ClaimName}' is invalid because it does not contain any of the valid values."
                );
                return ValueTask.CompletedTask;
            }
        }
        else
        {
            var stringValue = property.ToString();
            if (!validValueSet.Contains(stringValue))
            {
                context.Fail(
                    requirement.FailureCode,
                    $"The claim '{requirement.ClaimName}' is invalid because it does not contain any of the valid values."
                );
                return ValueTask.CompletedTask;
            }
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
