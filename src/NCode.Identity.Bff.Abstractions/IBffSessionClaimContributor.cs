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
/// Shapes the claims of a session once, at login: after the ID token validates and before the login gates run, so a
/// gate judges the session as it will be created. A good place for an expensive lookup, such as roles from a
/// directory, because the session keeps the result. Every registered contributor runs, in registration order.
/// </summary>
// See ADR-0290.
[PublicAPI]
public interface IBffSessionClaimContributor
{
    /// <summary>
    /// Adds or removes claims of the session about to be created.
    /// </summary>
    /// <param name="context">The context of the login, which holds the claims.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the contributor is done.</returns>
    ValueTask ContributeAsync(BffSessionClaimContext context, CancellationToken cancellationToken);
}
