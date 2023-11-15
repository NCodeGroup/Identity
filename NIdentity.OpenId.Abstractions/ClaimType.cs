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
/// Specifies the representations of Claim Values supported by the OpenID Provider.
/// https://openid.net/specs/openid-connect-core-1_0.html#ClaimTypes
/// </summary>
[TypeConverter(typeof(OpenIdEnumConverter<ClaimType>))]
public enum ClaimType
{
    /// <summary>
    /// Claims that are directly asserted by the OpenID Provider.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ClaimTypes.Normal)]
    Normal = 0,

    /// <summary>
    /// Claims that are asserted by a Claims Provider other than the OpenID Provider but are returned by OpenID Provider.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ClaimTypes.Aggregated)]
    Aggregated,

    /// <summary>
    /// Claims that are asserted by a Claims Provider other than the OpenID Provider but are returned as references by the OpenID Provider.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.ClaimTypes.Distributed)]
    Distributed
}
