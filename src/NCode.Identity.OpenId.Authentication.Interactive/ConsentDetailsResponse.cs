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
/// Describes a single scope on an interactive consent or authorized-applications surface.
/// </summary>
/// <param name="Value">The raw scope value.</param>
/// <param name="Description">A friendly description of the scope, or <see langword="null"/> when none is defined.</param>
/// <param name="Granted">Whether the subject has already granted this scope (so the consent surface can pre-select it).</param>
internal sealed record ConsentScopeResponse(string Value, string? Description, bool Granted);

/// <summary>
/// The JSON payload the consent page fetches to render the pending authorization request.
/// </summary>
/// <param name="ClientId">The identifier of the client requesting access.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured.</param>
/// <param name="Scopes">The scopes the client is requesting, each with a friendly description and already-granted state.</param>
internal sealed record ConsentDetailsResponse(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScopeResponse> Scopes
);
