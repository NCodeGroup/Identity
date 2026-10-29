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

using System.Security.Claims;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The default <see cref="IBffSessionFeature"/>, which holds the session that a request resolved.
/// </summary>
/// <param name="metadata">The metadata of the resolved session.</param>
/// <param name="principal">The principal built from the resolved session.</param>
internal sealed class BffSessionFeature(BffSessionMetadata metadata, ClaimsPrincipal principal)
    : IBffSessionFeature
{
    /// <inheritdoc />
    public BffSessionMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public ClaimsPrincipal Principal { get; } = principal;
}
