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
            context.Fail("The assertion requirement was not satisfied.");
        }
    }
}
