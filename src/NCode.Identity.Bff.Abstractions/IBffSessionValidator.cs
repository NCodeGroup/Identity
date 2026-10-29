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

namespace NCode.Identity.Bff;

/// <summary>
/// Decides whether an existing session may continue to be used.
/// </summary>
/// <remarks>
/// Validators run when a request resolves a session. A validator can only end a session; every registered validator
/// must keep it.
/// </remarks>
[PublicAPI]
public interface IBffSessionValidator
{
    /// <summary>
    /// Decides whether the session may continue.
    /// </summary>
    /// <param name="context">The context of the request that resolved the session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> to keep the session; otherwise, <see langword="false"/> to end it as
    /// <c>ended_by_validator</c>.
    /// </returns>
    ValueTask<bool> ValidateAsync(
        BffSessionValidationContext context,
        CancellationToken cancellationToken
    );
}
