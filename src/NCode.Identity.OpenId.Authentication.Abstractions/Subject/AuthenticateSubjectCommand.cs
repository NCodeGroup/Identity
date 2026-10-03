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
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents a mediator command to authenticate the subject (aka end-user) of the current HTTP request from the
/// credentials it presents (for example, a bearer access token), returning an <see cref="AuthenticateSubjectDisposition"/>.
/// This is the resource-facing counterpart to the interactive authorization-endpoint authentication and is reused by
/// endpoints such as <c>UserInfo</c>.
/// </summary>
[PublicAPI]
public readonly record struct AuthenticateSubjectCommand(OpenIdContext OpenIdContext)
    : ICommand<AuthenticateSubjectDisposition>;
