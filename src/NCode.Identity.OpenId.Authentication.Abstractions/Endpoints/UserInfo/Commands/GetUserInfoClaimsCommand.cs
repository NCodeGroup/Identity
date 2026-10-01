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
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;

/// <summary>
/// Represents a mediator command to contribute claims about the authenticated subject to a <c>UserInfo</c> response.
/// Multiple handlers may run; the default handler supplies the required <c>sub</c> claim, and applications register
/// additional handlers to add profile, email, and other claims (typically gated by the granted scopes).
/// </summary>
[PublicAPI]
public readonly record struct GetUserInfoClaimsCommand(
    OpenIdContext OpenIdContext,
    SubjectAuthentication SubjectAuthentication,
    IDictionary<string, object> Claims
) : ICommand;
