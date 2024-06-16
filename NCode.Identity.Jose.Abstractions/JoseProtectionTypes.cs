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

using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Constants for <see cref="CompactJwt.ProtectionType"/> that can be used to indicate how the JWT is protected.
/// </summary>
[PublicAPI]
public static class JoseProtectionTypes
{
    /// <summary>
    /// A value that indicates the JWT is protected using <c>JWS</c> (signing).
    /// </summary>
    public const string Jws = "JWS";

    /// <summary>
    /// A value that indicates the JWT is protected using <c>JWE</c> (encryption).
    /// </summary>
    public const string Jwe = "JWE";
}
