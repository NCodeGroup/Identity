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
/// Adds computed fields to the <c>/bff/user</c> response, beside its reserved keys, on every request for it. Every
/// registered contributor runs, in registration order.
/// </summary>
// See ADR-0290.
[PublicAPI]
public interface IBffUserContributor
{
    /// <summary>
    /// Adds fields to the response.
    /// </summary>
    /// <param name="context">The context of the request, which collects the fields.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the contributor is done.</returns>
    ValueTask ContributeAsync(BffUserContext context, CancellationToken cancellationToken);
}
