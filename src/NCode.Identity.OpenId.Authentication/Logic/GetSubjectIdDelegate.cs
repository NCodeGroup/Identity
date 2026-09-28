#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Represents a delegate that extracts the subject id from a <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <param name="subject">The <see cref="ClaimsPrincipal"/> to search for the subject id.</param>
/// <returns>The subject id if found; otherwise <c>null</c>.</returns>
public delegate string? GetSubjectIdDelegate(ClaimsPrincipal subject);
