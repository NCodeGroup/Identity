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
/// Describes the pending authorization request an interactive consent surface must present to the end-user: the client
/// requesting access and the scopes it is requesting. It is resolved (without consuming) from the continuation that the
/// authorize endpoint parked when it redirected to the consent page.
/// </summary>
/// <param name="ClientId">The natural identifier of the client requesting access.</param>
/// <param name="RequestedScopes">The scopes the client is requesting.</param>
[PublicAPI]
public readonly record struct PendingConsent(
    string ClientId,
    IReadOnlyList<string> RequestedScopes
);
