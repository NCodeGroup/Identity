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
using JetBrains.Annotations;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;

/// <summary>
/// Represents a mediator command to create the subject (a <see cref="ClaimsPrincipal"/>) for a resource-owner password
/// grant from a validated <see cref="LocalAccount"/>. The default handler assembles the account's self-issued
/// connection claims and its profile claims; applications replace it to customize the subject (for example, to add
/// authentication-context claims such as <c>amr</c> or <c>auth_time</c>).
/// </summary>
[PublicAPI]
public readonly record struct CreatePasswordGrantSubjectCommand(
    OpenIdContext OpenIdContext,
    LocalAccount Account
) : ICommand<ClaimsPrincipal>;
