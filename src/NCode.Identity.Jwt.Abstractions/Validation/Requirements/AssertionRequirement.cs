// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// A requirement whose satisfaction is determined by an arbitrary caller-supplied assertion. This is the inline
/// escape hatch for bespoke validation that does not warrant its own reusable requirement/handler pair; the delegate
/// operates only on <see cref="JwtValidationContext"/> (an Abstractions type), so custom validators remain expressible
/// without referencing the implementation package. The assertion returns <c>true</c> when the token satisfies the
/// requirement and <c>false</c> otherwise.
/// </summary>
/// <param name="Assertion">The delegate that evaluates the decoded token.</param>
[PublicAPI]
public sealed record AssertionRequirement(
    Func<JwtValidationContext, CancellationToken, ValueTask<bool>> Assertion
) : IJwtValidationRequirement;
