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
/// Evaluates one or more <see cref="IJwtValidationRequirement"/> instances against a decoded JSON Web Token (JWT).
/// Modeled on <c>Microsoft.AspNetCore.Authorization.IAuthorizationHandler</c>: handlers are resolved from dependency
/// injection and vote on each pending requirement by calling <see cref="JwtValidationContext.Succeed"/> or
/// <see cref="JwtValidationContext.Fail"/>. External code contributes custom validators by implementing this
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
