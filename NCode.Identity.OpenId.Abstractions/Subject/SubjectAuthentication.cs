#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.AspNetCore.Authentication;

namespace NCode.Identity.OpenId.Subject;

/// <summary>
/// Contains the contextual information about the subject (aka end-user) and how the subject was authenticated.
/// </summary>
[PublicAPI]
public readonly record struct SubjectAuthentication(
    string AuthenticationScheme,
    AuthenticationProperties AuthenticationProperties,
    ClaimsPrincipal Subject,
    string SubjectId
);
