// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="AssertionRequirement"/> by invoking the caller-supplied assertion delegate.
/// </summary>
internal sealed class AssertionRequirementHandler : JwtValidationHandler<AssertionRequirement>
{
    /// <inheritdoc />
    protected override async ValueTask HandleRequirementAsync(
        JwtValidationContext context,
        AssertionRequirement requirement,
        CancellationToken cancellationToken
    )
    {
        if (await requirement.Assertion(context, cancellationToken))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(
                JwtValidationFailureCodes.AssertionFailed,
                "The assertion requirement was not satisfied."
            );
        }
    }
}
