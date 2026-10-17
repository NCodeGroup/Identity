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
