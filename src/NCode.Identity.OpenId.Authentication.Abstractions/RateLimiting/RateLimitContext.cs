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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Carries what a <see cref="RateLimitDescriptor"/> needs to decide whether its limit applies to a request and which
/// partition of the limit the request counts in.
/// </summary>
// See ADR-0246.
[PublicAPI]
public readonly record struct RateLimitContext
{
    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> of the request.
    /// </summary>
    public required OpenIdContext OpenIdContext { get; init; }

    /// <summary>
    /// Gets the confidential <see cref="OpenIdClient"/> the request authenticated as, or <see langword="null"/> for a
    /// stage that is judged before a client authenticates.
    /// </summary>
    public OpenIdClient? Client { get; init; }
}
