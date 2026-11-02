// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    /// calling <see cref="JwtValidationContext.Succeed"/> when satisfied or <see cref="JwtValidationContext.Fail(string, string)"/> otherwise.
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
