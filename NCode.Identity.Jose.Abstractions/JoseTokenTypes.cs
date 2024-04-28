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

namespace NCode.Identity.Jose;

/// <summary>
/// Constants for the possible values that can be used in the <c>typ</c> JOSE header parameter.
/// https://datatracker.ietf.org/doc/html/rfc7515#section-4.1.9
/// </summary>
public static class JoseTokenTypes
{
    /// <summary>
    /// Specifies that the content is a <c>JWT</c>.
    /// https://datatracker.ietf.org/doc/html/rfc7519#section-5.1
    /// </summary>
    public const string Jwt = "JWT";

    /// <summary>
    /// Specifies that the content is a <c>JWT access token</c>.
    /// https://datatracker.ietf.org/doc/html/rfc9068#section-2.1
    /// </summary>
    public const string AccessTokenJwt = "at+jwt";
}
