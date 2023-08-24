#region Copyright Preamble

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

namespace NCode.Jose.Jwt;

/// <summary>
/// Constants that indicate how the <see cref="Claim.Value"/> should be evaluated.
/// </summary>
public static class JsonClaimValueTypes
{
    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// When creating a <see cref="Claim"/> the <see cref="Claim.Value"/> cannot be null. If the Json value was null,
    /// then the <see cref="Claim.Value"/> will be set to <see cref="string.Empty"/> and the <see cref="Claim.ValueType"/>
    /// will be set to <c>NULL</c>.
    /// </remarks>
    public const string Null = "NULL";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a Json object.
    /// </summary>
    /// <remarks>When creating a <see cref="Claim"/> from Json if the value was not a simple type {String, Null, True, False, Number}
    /// then <see cref="Claim.Value"/> will contain the Json value. If the Json was a JsonObject, the <see cref="Claim.ValueType"/> will be set to "JSON".</remarks>
    public const string Json = "JSON";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a Json object.
    /// </summary>
    /// <remarks>When creating a <see cref="Claim"/> from Json if the value was not a simple type {String, Null, True, False, Number}
    /// then <see cref="Claim.Value"/> will contain the Json value. If the Json was a JsonArray, the <see cref="Claim.ValueType"/> will be set to "JSON_ARRAY".</remarks>
    public const string JsonArray = "JSON_ARRAY";
}
