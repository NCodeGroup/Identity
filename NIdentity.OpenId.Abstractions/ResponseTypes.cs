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

using System.ComponentModel;

namespace NIdentity.OpenId;

/// <summary>
/// Specifies the desired authorization processing flow, including what parameters are returned from the endpoints
/// used.
/// </summary>
[Flags]
[TypeConverter(typeof(OpenIdEnumConverter<ResponseTypes>))]
public enum ResponseTypes
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    [Browsable(false)]
    Unspecified = 0,

    /// <summary>
    /// Specifies that the Authorization Server SHOULD NOT return an OAuth 2.0 Authorization Code, Access Token,
    /// Access Token Type, or ID Token in a successful response to the grant request.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ResponseTypes.None)]
    None = 1,

    /// <summary>
    /// Specifies that the the Authorization Server MUST return an Authorization Code in a successful response to
    /// the grant request.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ResponseTypes.Code)]
    Code = 2,

    /// <summary>
    /// Specifies that the the Authorization Server MUST return an ID Token in a successful response to the grant
    /// request.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ResponseTypes.IdToken)]
    IdToken = 4,

    /// <summary>
    /// Specifies that the the Authorization Server MUST return an Access Token in a successful response to the
    /// grant request.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ResponseTypes.Token)]
    Token = 8
}
