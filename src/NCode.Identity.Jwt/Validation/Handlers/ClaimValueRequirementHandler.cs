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
using System.Text.Json;
using NCode.Json;

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
            context.Fail($"The claim '{requirement.ClaimName}' is missing.");
            return ValueTask.CompletedTask;
        }

        if (property.IsNullOrUndefined())
        {
            context.Fail($"The claim '{requirement.ClaimName}' is null.");
            return ValueTask.CompletedTask;
        }

        // Snapshot the allowable values once: a frozen set gives O(1) ordinal lookups.
        var validValueSet = requirement.ValidValues.ToFrozenSet(StringComparer.Ordinal);

        if (property.ValueKind == JsonValueKind.Array)
        {
            if (!requirement.AllowCollection)
            {
                context.Fail(
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
                    $"The claim '{requirement.ClaimName}' is invalid because it does not contain any of the valid values."
                );
                return ValueTask.CompletedTask;
            }
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
