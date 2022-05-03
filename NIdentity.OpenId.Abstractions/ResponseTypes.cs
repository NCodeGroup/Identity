#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;

namespace NIdentity.OpenId;

/// <summary>
/// Specifies the desired authorization processing flow, including what parameters are returned from the endpoints
/// used.
/// </summary>
[Flags]
public enum ResponseTypes
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies that the Authorization Server SHOULD NOT return an OAuth 2.0 Authorization Code, Access Token,
    /// Access Token Type, or ID Token in a successful response to the grant request.
    /// </summary>
    None = 1,

    /// <summary>
    /// Specifies that the the Authorization Server MUST return an Authorization Code in a successful response to
    /// the grant request.
    /// </summary>
    Code = 2,

    /// <summary>
    /// Specifies that the the Authorization Server MUST return an ID Token in a successful response to the grant
    /// request.
    /// </summary>
    IdToken = 4,

    /// <summary>
    /// Specifies that the the Authorization Server MUST return an Access Token in a successful response to the
    /// grant request.
    /// </summary>
    Token = 8
}