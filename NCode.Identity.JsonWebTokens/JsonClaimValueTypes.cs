﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Constants for <see cref="Claim.ValueType"/> that can be used to indicate how <see cref="Claim.Value"/> should be interpreted.
/// </summary>
[PublicAPI]
public static class JsonClaimValueTypes
{
    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a <c>JSON</c> object.
    /// </summary>
    public const string Json = "JSON";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a <c>JSON</c> array.
    /// </summary>
    public const string JsonArray = "JSON_ARRAY";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is <c>null</c>.
    /// </summary>
    public const string JsonNull = "JSON_NULL";
}
