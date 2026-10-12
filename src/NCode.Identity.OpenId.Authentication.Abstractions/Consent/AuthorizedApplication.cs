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

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Describes one application a subject has granted standing consent to, for the self-service "authorized applications"
/// surface that lets an end-user review and revoke the access they have granted.
/// </summary>
/// <param name="ClientId">The natural identifier of the client the subject consented to.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured (the
/// surface falls back to the identifier).</param>
/// <param name="Scopes">The scopes the subject has granted to the client, with friendly descriptions when available.</param>
[PublicAPI]
public sealed record AuthorizedApplication(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScope> Scopes
);
