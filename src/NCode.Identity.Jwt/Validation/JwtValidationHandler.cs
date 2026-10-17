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
/// Provides a convenience base class for <see cref="IJwtValidationHandler"/> implementations that handle a single
/// requirement type. Modeled on <c>Microsoft.AspNetCore.Authorization.AuthorizationHandler{TRequirement}</c>: it
/// filters the pending requirements to <typeparamref name="TRequirement"/> and dispatches each to
/// <see cref="HandleRequirementAsync"/>. External code contributes custom validators by deriving from this class and
/// registering the handler.
/// </summary>
/// <typeparam name="TRequirement">The <see cref="IJwtValidationRequirement"/> type this handler evaluates.</typeparam>
[PublicAPI]
public abstract class JwtValidationHandler<TRequirement> : IJwtValidationHandler
    where TRequirement : IJwtValidationRequirement
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(
        JwtValidationContext context,
        CancellationToken cancellationToken
    )
    {
        foreach (var requirement in context.Requirements.OfType<TRequirement>())
        {
            await HandleRequirementAsync(context, requirement, cancellationToken);
        }
    }

    /// <summary>
    /// Evaluates a single <typeparamref name="TRequirement"/> against the decoded token in <paramref name="context"/>,
    /// calling <see cref="JwtValidationContext.Succeed"/> when satisfied or <see cref="JwtValidationContext.Fail"/> otherwise.
    /// </summary>
    /// <param name="context">The <see cref="JwtValidationContext"/> that carries the decoded token and voting state.</param>
    /// <param name="requirement">The requirement to evaluate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected abstract ValueTask HandleRequirementAsync(
        JwtValidationContext context,
        TRequirement requirement,
        CancellationToken cancellationToken
    );
}
