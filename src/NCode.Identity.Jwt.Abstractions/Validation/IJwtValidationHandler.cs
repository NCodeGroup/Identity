// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Evaluates one or more <see cref="IJwtValidationRequirement"/> instances against a decoded JSON Web Token (JWT).
/// Modeled on <c>Microsoft.AspNetCore.Authorization.IAuthorizationHandler</c>: handlers are resolved from dependency
/// injection and vote on each pending requirement by calling <see cref="JwtValidationContext.Succeed"/> or
/// <see cref="JwtValidationContext.Fail(string, string)"/>. External code contributes custom validators by implementing this
/// interface (typically via the <c>JwtValidationHandler&lt;TRequirement&gt;</c> base class) and registering it.
/// </summary>
[PublicAPI]
public interface IJwtValidationHandler
{
    /// <summary>
    /// Evaluates the requirements in <paramref name="context"/> that this handler understands.
    /// </summary>
    /// <param name="context">The <see cref="JwtValidationContext"/> that carries the decoded token and the pending requirements.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask HandleAsync(JwtValidationContext context, CancellationToken cancellationToken);
}
