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
using Microsoft.AspNetCore.Authorization;

namespace NCode.Identity.Bff;

/// <summary>
/// Marks a page of the BFF host that a browser reaches by top-level navigation, so a BFF session authenticates it on
/// <c>GET</c> and <c>HEAD</c> only, any other method answers <c>405</c>, and a challenge redirects to the BFF login.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffPageAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffPageAttribute"/> class.
    /// </summary>
    public BffPageAttribute()
    {
        AuthenticationSchemes = BffDefaults.AuthenticationScheme;
    }
}
