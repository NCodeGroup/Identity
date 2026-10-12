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

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Describes one application the end-user has granted standing consent to, for the authorized-applications page.
/// </summary>
/// <param name="ClientId">The identifier of the client.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured.</param>
/// <param name="Scopes">The scopes the subject has granted to the client, with friendly descriptions when available.</param>
internal sealed record AuthorizedApplicationResponse(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScopeResponse> Scopes
);

/// <summary>
/// The JSON payload the authorized-applications page fetches to render the end-user's authorized applications.
/// </summary>
/// <param name="Applications">The applications the end-user has granted standing consent to.</param>
internal sealed record AuthorizedApplicationsResponse(
    IReadOnlyList<AuthorizedApplicationResponse> Applications
);
